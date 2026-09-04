using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VDS.RDF;

namespace DesignPatternDetection.Detection;

/// <summary>Translates C# source code into an RDF graph of structural facts.</summary>
/// <remarks>The emitted vocabulary is declared in <c>docs/vocab.ttl</c>.</remarks>
public static class SourceGraphBuilder
{
    private const string VocabularyNamespace = "https://urga7.github.io/design-pattern-detection/vocab.ttl#";

    /// <summary>The instance data: one node per type and member of whatever was scanned.</summary>
    public const string ScanNamespace = "https://urga7.github.io/design-pattern-detection/scan#";

    private const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>
    /// Metadata references for the scan compilation: every assembly the current runtime trusts, so BCL types (like
    /// <c>IEnumerable</c>) resolve to real symbols. Assemblies that cannot be read are skipped.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(() =>
    {
        var references = new List<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string assemblies)
        {
            foreach (var path in assemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
                catch (Exception)
                {
                    // Unreadable assemblies are skipped; unresolved types degrade to simple names.
                }
            }
        }

        if (references.Count == 0)
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        return references;
    });

    public static SourceGraph Build(IEnumerable<string> filePaths)
    {
        var filePathsList = filePaths.ToList();
        Console.WriteLine($"Building source graph for {filePathsList.Count} file(s)...");
        var graph = new Graph();
        graph.NamespaceMap.AddNamespace("src", new Uri(VocabularyNamespace));
        graph.NamespaceMap.AddNamespace("scan", new Uri(ScanNamespace));
        graph.NamespaceMap.AddNamespace("rdf", new Uri(RdfNamespace));

        var trees = filePathsList
            .Select(filePath => CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath))
            .ToList();

        // Microsoft.NET.Sdk implicit usings
        trees.Add(CSharpSyntaxTree.ParseText("""
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """));

        // Sources only need to parse; compilation errors are not checked.
        var compilation = CSharpCompilation.Create(
            "SourceGraph",
            trees,
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        
        var memberIndices = new Dictionary<string, int>();
        var memberNodes = new Dictionary<ISymbol, INode>(SymbolEqualityComparer.Default);
        var bodies = new List<(INode Node, INode TypeNode, MemberDeclarationSyntax Member, SemanticModel Model)>();
        var locations = new Dictionary<string, SourceSpan>();
        
        // First every type's declaration facts
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var type in tree.GetCompilationUnitRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
                AddType(graph, model, type, memberIndices, memberNodes, bodies, locations);
        }

        // Then every body's behavioral facts
        var propertyNames = PropertyNames(memberNodes);
        foreach (var (node, typeNode, member, model) in bodies)
            AssertBodyFacts(graph, model, node, typeNode, member, memberNodes, propertyNames);

        Console.WriteLine("\tGraph built.");
        return new SourceGraph(graph, locations);
    }

    private static void AddType(
        IGraph graph,
        SemanticModel model,
        TypeDeclarationSyntax type,
        Dictionary<string, int> memberIndices,
        Dictionary<ISymbol, INode> memberNodes,
        List<(INode Node, INode TypeNode, MemberDeclarationSyntax Member, SemanticModel Model)> bodies,
        Dictionary<string, SourceSpan> locations
    )
    {
        var symbol = model.GetDeclaredSymbol(type);
        var typeFragment = symbol is null ? Sanitize(type.Identifier.Text) : Fragment(symbol);
        RecordLocation(locations, typeFragment, type.Identifier.GetLocation());
        var typeNode = FragmentNode(graph, typeFragment);

        var vocabularyNode = Node(graph, TypeKind(type));
        graph.Assert(typeNode, "rdf:type", vocabularyNode);
        AssertModifiers(graph, typeNode, type.Modifiers);
        
        foreach (var baseType in type.BaseList?.Types ?? default)
            graph.Assert(typeNode, "src:extends", TypeRef(graph, model, baseType.Type));

        var isInterface = type is InterfaceDeclarationSyntax;
        foreach (var member in type.Members)
            AddMember(graph, model, typeNode, typeFragment, member, isInterface, memberIndices, memberNodes, bodies, locations);

        if (symbol is not null)
            AssertInterfaceImplementations(graph, symbol, memberNodes);
    }

    private static void AddMember(
        IGraph graph,
        SemanticModel model,
        INode typeNode,
        string typeFragment,
        MemberDeclarationSyntax member,
        bool isInterface,
        Dictionary<string, int> memberIndices,
        Dictionary<ISymbol, INode> memberNodes,
        List<(INode Node, INode TypeNode, MemberDeclarationSyntax Member, SemanticModel Model)> bodies,
        Dictionary<string, SourceSpan> locations
    )
    {
        switch (member)
        {
            case ConstructorDeclarationSyntax ctor:
            {
                var node = MemberNode(graph, typeFragment, "Ctor", NextIndex(memberIndices, typeFragment), locations, ctor);
                graph.Assert(typeNode, "src:hasConstructor", node);
                graph.Assert(node, "rdf:type", Node(graph, "Constructor"));
                AssertModifiers(graph, node, ctor.Modifiers);
                AssertParameterTypes(graph, model, node, ctor.ParameterList);
                bodies.Add((node, typeNode, ctor, model));

                // C# constructors are private by omission
                if (model.GetDeclaredSymbol(ctor) is { DeclaredAccessibility: Accessibility.Private })
                    graph.Assert(node, "src:hasModifier", Node(graph, "Private"));
                
                break;
            }
            case MethodDeclarationSyntax method:
            {
                var node = MemberNode(graph, typeFragment, method.Identifier.Text, NextIndex(memberIndices, typeFragment), locations, method);
                graph.Assert(typeNode, "src:hasMethod", node);
                graph.Assert(node, "rdf:type", Node(graph, "Method"));
                graph.Assert(node, "src:returnsType", TypeRef(graph, model, method.ReturnType));
                AssertTypeArguments(graph, model, node, method.ReturnType);
                AssertModifiers(graph, node, method.Modifiers);
                AssertParameterTypes(graph, model, node, method.ParameterList);
                AssertInstantiations(graph, model, node, method);
                RegisterMember(graph, model, method, node, isInterface, memberNodes);
                bodies.Add((node, typeNode, method, model));
                break;
            }
            case PropertyDeclarationSyntax property:
            {
                var node = MemberNode(graph, typeFragment, property.Identifier.Text, NextIndex(memberIndices, typeFragment), locations, property);
                graph.Assert(typeNode, "src:hasProperty", node);
                graph.Assert(node, "rdf:type", Node(graph, "Property"));
                graph.Assert(node, "src:returnsType", TypeRef(graph, model, property.Type));
                AssertTypeArguments(graph, model, node, property.Type);
                AssertModifiers(graph, node, property.Modifiers);
                RegisterMember(graph, model, property, node, isInterface, memberNodes);
                bodies.Add((node, typeNode, property, model));
                break;
            }
            case FieldDeclarationSyntax field:
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    var node = MemberNode(graph, typeFragment, variable.Identifier.Text, NextIndex(memberIndices, typeFragment), locations, variable);
                    graph.Assert(typeNode, "src:hasField", node);
                    graph.Assert(node, "rdf:type", Node(graph, "Field"));
                    graph.Assert(node, "src:returnsType", TypeRef(graph, model, field.Declaration.Type));
                    AssertTypeArguments(graph, model, node, field.Declaration.Type);
                    AssertModifiers(graph, node, field.Modifiers);
                    if (model.GetDeclaredSymbol(variable) is { } fieldSymbol)
                        memberNodes[fieldSymbol] = node;
                }

                break;
            }
        }
    }

    /// <summary>
    /// Records the member's symbol-to-node mapping and asserts the <c>Abstract</c> that a bodiless interface
    /// member implies. Default interface methods stay non-abstract.
    /// </summary>
    private static void RegisterMember(
        IGraph graph,
        SemanticModel model,
        MemberDeclarationSyntax member,
        INode node,
        bool isInterface,
        Dictionary<ISymbol, INode> memberNodes
    )
    {
        if (model.GetDeclaredSymbol(member) is not { } symbol)
            return;

        memberNodes[symbol] = node;

        if (isInterface && symbol.IsAbstract)
            graph.Assert(node, "src:hasModifier", Node(graph, "Abstract"));
    }

    /// <summary>
    /// Asserts <c>src:Override</c> on every member this type declares that implements - implicitly or explicitly -
    /// a member of any interface it implements, directly or by inheritance. Such members carry no <c>override</c>
    /// token of their own.
    /// </summary>
    private static void AssertInterfaceImplementations(
        IGraph graph,
        INamedTypeSymbol type,
        Dictionary<ISymbol, INode> memberNodes
    )
    {
        foreach (var interfaceType in type.AllInterfaces)
        {
            foreach (var interfaceMember in interfaceType.GetMembers())
            {
                if (interfaceMember is not (IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol))
                    continue;

                var implementation = type.FindImplementationForInterfaceMember(interfaceMember);
                if (implementation is not null
                    && SymbolEqualityComparer.Default.Equals(implementation.ContainingType, type)
                    && memberNodes.TryGetValue(implementation, out var node))
                {
                    graph.Assert(node, "src:hasModifier", Node(graph, "Override"));
                }
            }
        }
    }

    /// <summary>Records each type argument and array element type of a member type, at every nesting depth.</summary>
    private static void AssertTypeArguments(IGraph graph, SemanticModel model, INode memberNode, TypeSyntax type) =>
        AssertTypeArguments(graph, memberNode, Unwrap(model.GetTypeInfo(type).Type));

    private static void AssertTypeArguments(IGraph graph, INode memberNode, ITypeSymbol? type)
    {
        switch (type)
        {
            case INamedTypeSymbol { TypeArguments.Length: > 0 } generic:
                foreach (var argument in generic.TypeArguments)
                {
                    graph.Assert(memberNode, "src:hasTypeArgument", TypeRef(graph, argument));
                    AssertTypeArguments(graph, memberNode, Unwrap(argument));
                }

                break;
            case IArrayTypeSymbol array:
                graph.Assert(memberNode, "src:hasTypeArgument", TypeRef(graph, array.ElementType));
                AssertTypeArguments(graph, memberNode, Unwrap(array.ElementType));
                break;
        }
    }

    private static void AssertParameterTypes(IGraph graph, SemanticModel model, INode memberNode, ParameterListSyntax parameters)
    {
        foreach (var parameter in parameters.Parameters)
        {
            if (parameter.Type is null) continue;
            graph.Assert(memberNode, "src:hasParameterType", TypeRef(graph, model, parameter.Type));
        }
    }

    private static void AssertInstantiations(IGraph graph, SemanticModel model, INode methodNode, MethodDeclarationSyntax method)
    {
        foreach (var creation in method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            graph.Assert(methodNode, "src:instantiates", TypeRef(graph, model, creation.Type));
    }

    /// <summary>
    /// Asserts the behavioral facts of a method, constructor or property-accessor body: <c>invokes</c>,
    /// <c>calls</c>, <c>delegatesTo</c>, <c>assignsField</c> and <c>returnsSelf</c>. <c>calls</c>,
    /// <c>delegatesTo</c> and <c>assignsField</c> are pinned to members declared in the scanned source, which is
    /// what <paramref name="memberNodes"/> holds.
    /// </summary>
    private static void AssertBodyFacts(
        IGraph graph,
        SemanticModel model,
        INode memberNode,
        INode typeNode,
        MemberDeclarationSyntax member,
        Dictionary<ISymbol, INode> memberNodes,
        HashSet<string> propertyNames
    )
    {
        foreach (var node in member.DescendantNodes())
        {
            switch (node)
            {
                case InvocationExpressionSyntax invocation:
                    AssertInvocation(graph, model, memberNode, typeNode, invocation, memberNodes);
                    break;
                
                case SimpleNameSyntax name when propertyNames.Contains(name.Identifier.Text) &&
                                                model.GetSymbolInfo(name).Symbol is IPropertySymbol property && 
                                                memberNodes.TryGetValue(property, out var propertyNode):
                    graph.Assert(memberNode, "src:calls", propertyNode);
                    break;

                case AssignmentExpressionSyntax assignment:
                    if (model.GetSymbolInfo(assignment.Left).Symbol is IFieldSymbol field && memberNodes.TryGetValue(field, out var fieldNode))
                    {
                        graph.Assert(memberNode, "src:assignsField", fieldNode);
                    }

                    break;

                case ReturnStatementSyntax { Expression: ThisExpressionSyntax }:
                    graph.Assert(memberNode, "src:returnsSelf", typeNode);
                    break;
            }
        }

        if (member is MethodDeclarationSyntax { ExpressionBody.Expression: ThisExpressionSyntax })
            graph.Assert(memberNode, "src:returnsSelf", typeNode);
    }

    /// <summary>
    /// The simple names under which the scanned source declares a property. An explicitly implemented property is
    /// named <c>N.IFoo.Bar</c> by its symbol but written <c>Bar</c> at the use site, so only the last segment is
    /// indexed.
    /// </summary>
    private static HashSet<string> PropertyNames(Dictionary<ISymbol, INode> memberNodes) =>
        memberNodes.Keys
            .OfType<IPropertySymbol>()
            .Select(property => property.Name[(property.Name.LastIndexOf('.') + 1)..])
            .ToHashSet(StringComparer.Ordinal);

    private static void AssertInvocation(
        IGraph graph,
        SemanticModel model,
        INode memberNode,
        INode typeNode,
        InvocationExpressionSyntax invocation,
        Dictionary<ISymbol, INode> memberNodes)
    {
        var callee = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        var receiver = Receiver(invocation);

        // MemberwiseClone() makes a new instance of the caller's own type without the 'new' keyword.
        if (callee is { Name: "MemberwiseClone", ContainingType.SpecialType: SpecialType.System_Object })
            graph.Assert(memberNode, "src:instantiates", typeNode);

        // The callee's declaring type, falling back to the receiver's declared type when the callee did not resolve.
        var calleeType = callee?.ContainingType ?? (receiver is null ? null : model.GetTypeInfo(receiver).Type);
        if (calleeType is not null)
            graph.Assert(memberNode, "src:invokes", TypeRef(graph, calleeType));

        // src:calls - a callee declared on the containing type itself.
        if (callee is not null && memberNodes.TryGetValue(callee, out var calleeNode))
            graph.Assert(memberNode, "src:calls", calleeNode);

        // src:delegatesTo - the receiver is one of the type's own fields or properties.
        var receiverSymbol = receiver is null ? null : model.GetSymbolInfo(receiver).Symbol;
        if (receiverSymbol is IFieldSymbol or IPropertySymbol
            && memberNodes.TryGetValue(receiverSymbol, out var wrappedNode))
        {
            graph.Assert(memberNode, "src:delegatesTo", wrappedNode);
        }
    }

    /// <summary>The expression a member call is made on: <c>x</c> in <c>x.M()</c>, <c>null</c> for plain <c>M()</c>.</summary>
    private static ExpressionSyntax? Receiver(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Expression,
        MemberBindingExpressionSyntax => invocation.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>()?.Expression,
        _ => null
    };

    private static void AssertModifiers(IGraph graph, INode subject, SyntaxTokenList modifiers)
    {
        foreach (var modifier in modifiers)
        {
            var name = ModifierName(modifier.Kind());
            if (name is null) continue;
            graph.Assert(subject, "src:hasModifier", Node(graph, name));
        }
    }

    private static string TypeKind(TypeDeclarationSyntax type) => type switch
    {
        InterfaceDeclarationSyntax => "Interface",
        StructDeclarationSyntax => "Struct",
        _ => "Class"
    };

    private static string? ModifierName(SyntaxKind kind) => kind switch
    {
        SyntaxKind.PublicKeyword => "Public",
        SyntaxKind.PrivateKeyword => "Private",
        SyntaxKind.ProtectedKeyword => "Protected",
        SyntaxKind.InternalKeyword => "Internal",
        SyntaxKind.StaticKeyword => "Static",
        SyntaxKind.AbstractKeyword => "Abstract",
        SyntaxKind.VirtualKeyword => "Virtual",
        SyntaxKind.OverrideKeyword => "Override",
        SyntaxKind.SealedKeyword => "Sealed",
        SyntaxKind.ReadOnlyKeyword => "ReadOnly",
        SyntaxKind.ConstKeyword => "Const",
        _ => null
    };

    private static INode MemberNode(
        IGraph graph,
        string typeFragment,
        string memberName,
        int index,
        Dictionary<string, SourceSpan> locations,
        SyntaxNode declaration
    )
    {
        var fragment = $"{typeFragment}_{Sanitize(memberName)}_{index}";
        RecordLocation(locations, fragment, declaration.GetLocation());
        return FragmentNode(graph, fragment);
    }

    /// <summary>
    /// Records where a node's declaration lives, 1-based and inclusive. Types record their identifier line, members
    /// their whole declaration; for a partial type the first declaration in file order wins. Nodes from a tree with
    /// no path are skipped.
    /// </summary>
    private static void RecordLocation(Dictionary<string, SourceSpan> locations, string fragment, Location location)
    {
        var span = location.GetLineSpan();
        if (string.IsNullOrEmpty(span.Path))
            return;

        locations.TryAdd(
            fragment,
            new SourceSpan(span.Path, span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
    }

    /// <summary>The next member index for a type, counted across the whole build so the parts of a partial type
    /// cannot collide on member node names.</summary>
    private static int NextIndex(Dictionary<string, int> memberIndices, string typeFragment)
    {
        memberIndices.TryGetValue(typeFragment, out var index);
        memberIndices[typeFragment] = index + 1;
        return index;
    }

    /// <summary>A node for a type, identified by its resolved symbol so declarations and uses unify.</summary>
    private static INode TypeRef(IGraph graph, SemanticModel model, TypeSyntax type)
    {
        var symbol = model.GetTypeInfo(type).Type;
        return FragmentNode(graph, symbol is null ? SimpleName(type.ToString()) : Fragment(symbol));
    }

    private static INode TypeRef(IGraph graph, ITypeSymbol type) => FragmentNode(graph, Fragment(type));

    /// <summary>
    /// The graph identity of a resolved type: its dotted namespace and containing-type-qualified name. Unresolved
    /// (error) types keep just the written simple name; predefined types keep their C# keyword spelling; nullable
    /// value types take their underlying type's.
    /// </summary>
    private static string Fragment(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return Fragment(array.ElementType) + "__"; // Sanitize("[]")
            case INamedTypeSymbol named:
            {
                if (IsNullableValueType(named))
                    return Fragment(named.TypeArguments[0]);
                
                if (Keyword(named) is { } keyword)
                    return keyword;

                var local = LocalName(named);
                if (named.TypeKind == Microsoft.CodeAnalysis.TypeKind.Error)
                    return local;

                var prefix = ContainerPrefix(named);
                return prefix.Length == 0 ? local : prefix + "." + local;
            }
            default:
                return type.Name.Length == 0 ? "_" : Sanitize(type.Name);
        }
    }

    private static string LocalName(INamedTypeSymbol named) =>
        Sanitize(named.TypeArguments.Length == 0
            ? named.Name
            : named.Name + "<" + string.Join(",", named.TypeArguments.Select(ArgumentName)) + ">");

    private static string ArgumentName(ITypeSymbol argument) => argument switch
    {
        IArrayTypeSymbol array => ArgumentName(array.ElementType) + "[]",
        _ => Keyword(argument) ?? argument.Name
    };

    private static string ContainerPrefix(INamedTypeSymbol named)
    {
        var segments = new List<string>();
        for (var container = named.ContainingType; container is not null; container = container.ContainingType)
            segments.Insert(0, Sanitize(container.Name));
        for (var ns = named.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
            segments.Insert(0, Sanitize(ns.Name));
        return string.Join(".", segments);
    }

    private static bool IsNullableValueType(INamedTypeSymbol named) =>
        named is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 };

    private static ITypeSymbol? Unwrap(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && IsNullableValueType(named) ? named.TypeArguments[0] : type;

    /// <summary>C# keyword spelling for predefined types, matching how source writes them.</summary>
    private static string? Keyword(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Void => "void",
        SpecialType.System_Object => "object",
        SpecialType.System_Boolean => "bool",
        SpecialType.System_Char => "char",
        SpecialType.System_SByte => "sbyte",
        SpecialType.System_Byte => "byte",
        SpecialType.System_Int16 => "short",
        SpecialType.System_UInt16 => "ushort",
        SpecialType.System_Int32 => "int",
        SpecialType.System_UInt32 => "uint",
        SpecialType.System_Int64 => "long",
        SpecialType.System_UInt64 => "ulong",
        SpecialType.System_Decimal => "decimal",
        SpecialType.System_Single => "float",
        SpecialType.System_Double => "double",
        SpecialType.System_String => "string",
        _ => null
    };

    /// <summary>Creates an instance node - a scanned type or member - from a raw fragment.</summary>
    private static INode FragmentNode(IGraph graph, string fragment) =>
        graph.CreateUriNode(new Uri(ScanNamespace + fragment));

    /// <summary>Creates a vocabulary node - one of the fixed terms declared in <c>docs/vocab.ttl</c>.</summary>
    private static INode Node(IGraph graph, string localName) => graph.CreateUriNode("src:" + Sanitize(localName));

    private static string SimpleName(string typeName)
    {
        var name = typeName.TrimEnd('?'); // drop nullable marker
        var lastDot = name.LastIndexOf('.'); // drop namespace qualifier
        return Sanitize(lastDot >= 0 ? name[(lastDot + 1)..] : name);
    }

    /// <summary>Replaces any character that is invalid in a URI local name with '_'.</summary>
    private static string Sanitize(string value) =>
        new(value.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
}
