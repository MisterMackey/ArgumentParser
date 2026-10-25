using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Links the nodes of a compilation into trees. A property whose type is a node is a child command of the class that declares it.
/// Linking works on plain data without locations, so an edit that only moves code does not link again.
/// The rules that need the whole tree (duplicate names, a command with two parents, unreachable commands, argument names
/// that a command above uses) are checked here
/// and described as <see cref="LinkProblem"/>s, which <see cref="ToDiagnostics"/> turns into diagnostics with their locations.
/// </summary>
public static class CommandTreeLinker
{
	private sealed class Edge
	{
		public Edge(CommandNodeSource parent, ChildCandidate property, CommandNodeSource target)
		{
			Parent = parent;
			Property = property;
			Target = target;
		}

		public CommandNodeSource Parent { get; }
		public ChildCandidate Property { get; }
		public CommandNodeSource Target { get; }
	}

	/// <summary>
	/// Links the nodes into trees, one for every root, and finds the rules they break.
	/// </summary>
	/// <param name="sources">The nodes of the compilation, in any order.</param>
	/// <returns>The trees and the problems.</returns>
	public static LinkResult Link(IEnumerable<CommandNodeSource> sources)
	{
		if (sources is null)
		{
			throw new ArgumentNullException(nameof(sources));
		}

		// a class that is declared in several parts can come in more than once, sorting keeps the result independent of the order
		var nodes = sources
			.GroupBy(s => s.Node.TypeName, StringComparer.Ordinal)
			.Select(g => g.First())
			.OrderBy(s => s.Node.TypeName, StringComparer.Ordinal)
			.ToList();
		var byType = nodes.ToDictionary(n => n.Node.TypeName, StringComparer.Ordinal);

		// An argument attribute on a property of a command class type is an error (ARG026), reported when the class is read.
		// Such a property is not linked as a child. Its class is left out of the rule about a group without children (ARG020),
		// and its type out of the rule about an unreachable command (ARG019): they would only repeat that error.
		var edgesByParent = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
		var argumentOwners = new HashSet<string>(StringComparer.Ordinal);
		var argumentTypes = new HashSet<string>(StringComparer.Ordinal);
		foreach (var node in nodes)
		{
			var edges = new List<Edge>();
			for (int i = 0; i < node.Node.Children.Count; i++)
			{
				var candidate = node.Node.Children[i];
				if (!byType.TryGetValue(candidate.TypeName, out var target))
				{
					continue;
				}
				if (candidate.HasArgumentAttribute)
				{
					argumentOwners.Add(node.Node.TypeName);
					argumentTypes.Add(target.Node.TypeName);
				}
				else
				{
					edges.Add(new Edge(node, candidate, target));
				}
			}
			edgesByParent[node.Node.TypeName] = edges;
		}

		var problems = new List<LinkProblem>();
		var errorTypes = new HashSet<string>(StringComparer.Ordinal);
		void Error(LinkProblem problem, params string[] related)
		{
			problems.Add(problem);
			errorTypes.Add(problem.TypeName);
			foreach (var type in related)
			{
				errorTypes.Add(type);
			}
		}

		var parentCount = edgesByParent.Values.SelectMany(e => e).GroupBy(e => e.Target.Node.TypeName, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

		foreach (var node in nodes)
		{
			var info = node.Node;
			var edges = edgesByParent[info.TypeName];
			if (info.Kind != CommandNodeKind.Leaf && edges.Count == 0 && !argumentOwners.Contains(info.TypeName))
			{
				Error(new LinkProblem(GeneratorDiagnostics.ARG020, info.TypeName, null, Args(info.ClassName)));
			}
			foreach (var edge in edges)
			{
				if (info.Kind == CommandNodeKind.Leaf)
				{
					Error(new LinkProblem(GeneratorDiagnostics.ARG021, info.TypeName, edge.Property.PropertyName, Args(info.ClassName, edge.Property.PropertyName)));
				}
				if (!edge.Property.HasSetter)
				{
					Error(new LinkProblem(GeneratorDiagnostics.ARG022, info.TypeName, edge.Property.PropertyName, Args(info.ClassName, edge.Property.PropertyName)));
				}
				var target = edge.Target;
				var parents = parentCount[target.Node.TypeName];
				if (parents > 1 || target.Node.Kind == CommandNodeKind.Root)
				{
					Error(
						new LinkProblem(GeneratorDiagnostics.ARG018, info.TypeName, edge.Property.PropertyName, Args(target.Node.ClassName, info.ClassName, edge.Property.PropertyName)),
						target.Node.TypeName);
				}
			}
			// the same class in two properties is one command declared twice, which ARG018 reports
			foreach (var sameName in edges.GroupBy(e => e.Target.Node.Name, StringComparer.Ordinal)
				.Where(g => g.Select(e => e.Target.Node.TypeName).Distinct(StringComparer.Ordinal).Count() > 1))
			{
				foreach (var edge in sameName)
				{
					Error(new LinkProblem(GeneratorDiagnostics.ARG017, info.TypeName, edge.Property.PropertyName, Args(sameName.Key, info.ClassName)));
				}
			}
		}

		var trees = new List<CommandTree>();
		var reached = new HashSet<string>(StringComparer.Ordinal);
		foreach (var root in nodes.Where(n => n.Node.Kind == CommandNodeKind.Root))
		{
			var treeNodes = new List<TreeNode>();
			var inTree = new HashSet<string>(StringComparer.Ordinal);
			var ancestors = new List<CommandNodeSource>();
			void Visit(CommandNodeSource node, IReadOnlyList<string> path)
			{
				if (!inTree.Add(node.Node.TypeName))
				{
					return;
				}
				reached.Add(node.Node.TypeName);
				// a node in two trees (ARG018) would be checked twice
				foreach (var problem in NamesUsedAbove(node, ancestors).Where(p => !problems.Contains(p)))
				{
					Error(problem);
				}
				var edges = edgesByParent[node.Node.TypeName];
				treeNodes.Add(new TreeNode(
					node,
					new EquatableArray<string>(path),
					new EquatableArray<TreeChild>(edges.Select(e => new TreeChild(e.Property.PropertyName, e.Target.Node.TypeName, e.Target.Node.Name)))));
				ancestors.Add(node);
				foreach (var edge in edges)
				{
					Visit(edge.Target, path.Concat(new[] { edge.Target.Node.Name }).ToList());
				}
				ancestors.RemoveAt(ancestors.Count - 1);
			}
			Visit(root, Array.Empty<string>());
			var hasErrors = treeNodes.Any(n => n.Source.HasErrors || errorTypes.Contains(n.Source.Node.TypeName));
			trees.Add(new CommandTree(new EquatableArray<TreeNode>(treeNodes), hasErrors));
		}

		foreach (var node in nodes.Where(n => n.Node.Kind != CommandNodeKind.Root && !reached.Contains(n.Node.TypeName) && !argumentTypes.Contains(n.Node.TypeName)))
		{
			problems.Add(new LinkProblem(GeneratorDiagnostics.ARG019, node.Node.TypeName, null, Args(node.Node.ClassName)));
		}

		return new LinkResult(new EquatableArray<CommandTree>(trees), new EquatableArray<LinkProblem>(problems));
	}

	/// <summary>
	/// Turns the problems of a link into diagnostics, with the locations of the classes and properties they are about.
	/// </summary>
	/// <param name="result">The result of linking.</param>
	/// <param name="models">The models of the nodes, which know where everything is declared.</param>
	/// <returns>The diagnostics.</returns>
	public static EquatableArray<DiagnosticInfo> ToDiagnostics(LinkResult result, IEnumerable<CommandNodeModel> models)
	{
		if (result is null)
		{
			throw new ArgumentNullException(nameof(result));
		}
		if (models is null)
		{
			throw new ArgumentNullException(nameof(models));
		}
		var byType = new Dictionary<string, CommandNodeModel>(StringComparer.Ordinal);
		foreach (var model in models)
		{
			if (!byType.ContainsKey(model.Node.TypeName))
			{
				byType.Add(model.Node.TypeName, model);
			}
		}

		var diagnostics = new List<DiagnosticInfo>();
		for (int i = 0; i < result.Problems.Count; i++)
		{
			var problem = result.Problems[i];
			LocationInfo? location = null;
			if (byType.TryGetValue(problem.TypeName, out var model))
			{
				location = model.ClassLocation;
				if (problem.PropertyName is not null)
				{
					// a problem about a property is about a child command, or (ARG027) about an option or flag
					var property = model.ChildLocations.AsReadOnly().Concat(model.ArgumentLocations.AsReadOnly())
						.FirstOrDefault(p => p.PropertyName == problem.PropertyName);
					location = property?.Location ?? location;
				}
			}
			diagnostics.Add(DiagnosticInfo.FromLocationInfo(problem.Descriptor, location, problem.Arguments.AsReadOnly()));
		}
		return new EquatableArray<DiagnosticInfo>(diagnostics);
	}

	// A short or long name of an option or flag must not be used by any command above: siblings may share names, ancestors may not.
	// The nearest command above that uses the name is the one reported.
	private static IEnumerable<LinkProblem> NamesUsedAbove(CommandNodeSource node, IReadOnlyList<CommandNodeSource> ancestors)
	{
		foreach (var argument in ArgumentsOf(node))
		{
			foreach (var (kind, name) in new[] { ("short", argument.Attribute.ShortName), ("long", argument.Attribute.LongName) })
			{
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}
				var owner = ancestors.LastOrDefault(a => ArgumentsOf(a).Any(other => (kind == "short" ? other.Attribute.ShortName : other.Attribute.LongName) == name));
				if (owner is not null)
				{
					yield return new LinkProblem(
						GeneratorDiagnostics.ARG027,
						node.Node.TypeName,
						argument.PropertyName,
						Args(node.Node.ClassName, argument.PropertyName, kind, name, owner.Node.ClassName));
				}
			}
		}
	}

	private static IEnumerable<PropertyAndAttributeInfo> ArgumentsOf(CommandNodeSource node)
	{
		return node.Arguments.Options.AsReadOnly().Concat(node.Arguments.Flags.AsReadOnly());
	}

	private static EquatableArray<string> Args(params string[] arguments)
	{
		return new EquatableArray<string>(arguments);
	}
}
