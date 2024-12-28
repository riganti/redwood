using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.Runtime.Tracing;
using DotVVM.Framework.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.Framework.Compilation.ControlTree
{
    public class DefaultControlResolver : ControlResolverBase
    {
        private readonly DotvvmConfiguration configuration;
        private readonly IControlBuilderFactory controlBuilderFactory;
        private readonly CompiledAssemblyCache compiledAssemblyCache;
        private readonly Dictionary<string, Type>? controlNameMappings;

        private static readonly object locker = new object();
        private static volatile bool isInitialized = false;
        private static readonly object dotvvmLocker = new object();
        private static volatile bool isDotvvmInitialized = false;


        public DefaultControlResolver(DotvvmConfiguration configuration, IControlBuilderFactory controlBuilderFactory, CompiledAssemblyCache compiledAssemblyCache) : base(configuration.Markup)
        {
            this.configuration = configuration;
            this.controlBuilderFactory = controlBuilderFactory;
            this.compiledAssemblyCache = compiledAssemblyCache;

            if (!isInitialized)
            {
                lock (locker)
                {
                    if (!isInitialized)
                    {
                        var startupTracer = configuration.ServiceProvider.GetService<IStartupTracer>();

                        startupTracer?.TraceEvent(StartupTracingConstants.InvokeAllStaticConstructorsStarted);
                        InvokeStaticConstructorsOnAllControls();
                        ResolveAllPropertyAliases();
                        startupTracer?.TraceEvent(StartupTracingConstants.InvokeAllStaticConstructorsFinished);

                        isInitialized = true;
                    }
                }
            }

            controlNameMappings = BuildControlAliasesMap();
        }

        internal static Task InvokeStaticConstructorsOnDotvvmControls()
        {
            if (isDotvvmInitialized) return Task.CompletedTask;
            return Task.Run(() => {
                if (isDotvvmInitialized) return;
                lock(dotvvmLocker) {
                    if (isDotvvmInitialized) return;
                    InvokeStaticConstructorsOnAllControls(typeof(DotvvmControl).Assembly);
                    isDotvvmInitialized = true;
                }
            });
        }

        /// <summary>
        /// Invokes the static constructors on all controls to register all <see cref="DotvvmProperty"/>.
        /// </summary>
        private void InvokeStaticConstructorsOnAllControls()
        {
            var dotvvmInitTask = InvokeStaticConstructorsOnDotvvmControls();

            var dotvvmAssembly = typeof(DotvvmControl).Assembly.GetName().Name!;

            var assemblies = configuration.ExperimentalFeatures.ExplicitAssemblyLoading.Enabled ?
                // use only explicitly specified assemblies from configuration
                compiledAssemblyCache.GetReferencedAssemblies() :
                compiledAssemblyCache.GetAllAssemblies();
            InvokeStaticConstructorsOnAllControls(OrderAndFilterAssemblies(assemblies, dotvvmAssembly));
            dotvvmInitTask.Wait();
        }

        /// <summary> Filters out assemblies which don't reference DotVVM.Framework, and topologically orders them according to their references, then alphabetically to resolve ties </summary>
        private List<Assembly> OrderAndFilterAssemblies(IEnumerable<Assembly> assemblies, string rootAssembly)
        {
            var assemblyList = new List<Assembly>();
            var renumbering = new Dictionary<string, int>();
            var references = new List<AssemblyName[]>();

            var namelessAssemblies = new List<Assembly>(); // place them at the end
            foreach (var a in assemblies)
            {
                var name = a.GetName();
                var r = a.GetReferencedAssemblies();
                if (ReferencesAssembly(r, rootAssembly))
                {
                    if (name.Name is null)
                        namelessAssemblies.Add(a);
                    else if (renumbering.TryAdd(name.Name, assemblyList.Count))
                    {
                        assemblyList.Add(a);
                        references.Add(r);
                    }
                }
            }

            // Kahn's algorithm - https://en.wikipedia.org/wiki/Topological_sorting#Kahn's_algorithm
            // with additional sorting step to resolve ties
            var inDegree = new int[assemblyList.Count];
            var forwardReferences = new List<int>?[assemblyList.Count];
            var roots = new List<int>();
            for (int i = 0; i < references.Count; i++)
            {
                var inCount = 0;
                foreach (var r in references[i])
                    if (renumbering.TryGetValue(r.Name!, out var idx))
                    {
                        inCount++;
                        forwardReferences[idx] ??= new List<int>();
                        forwardReferences[idx]!.Add(i);
                    }
                inDegree[i] = inCount;
                if (inCount == 0)
                    roots.Add(i);
            }

            var sorted = new List<Assembly>(capacity: assemblyList.Count + namelessAssemblies.Count);
            var newRoots = new List<int>();
            var comparer = makeComparator(assemblyList);
            while (roots.Count > 0)
            {
                if (roots.Count > 1)
                    roots.Sort(comparer);

                foreach (var item in roots)
                {
                    sorted.Add(assemblyList[item]);
                    if (forwardReferences[item] is null)
                        continue;
                    foreach (var r in forwardReferences[item]!)
                    {
                        inDegree[r]--;
                        if (inDegree[r] == 0)
                            newRoots.Add(r);
                    }
                }
                roots.Clear();
                (roots, newRoots) = (newRoots, roots);
            }

            // no need to throw in production, we only want the topological ordering for consistent property IDs
            Debug.Assert(sorted.Count == assemblyList.Count, "Loop in assembly references detected");

            sorted.AddRange(namelessAssemblies);
            return sorted;
        
            static Comparison<int> makeComparator(List<Assembly> assemblyList) => (a, b) => string.Compare(assemblyList[a].GetName().Name, assemblyList[b].GetName().Name, StringComparison.Ordinal);
        }
        static bool ReferencesAssembly(AssemblyName[] references, string root)
        {
            foreach (var r in references)
                if (r.Name == root)
                    return true;
            return false;
        }

        private void InvokeStaticConstructorsOnAllControls(List<Assembly> assemblies)
        {
            // try to assign property IDs consistently across runs, as the order of properties depends on this which may be observable to the user
            // we first assigns IDs to all controls is each assembly, then we run the static constructors in parallel

            // this means we sequence the control registration, while parallelizing assembly loading and property registration
            // in practice, control registration is trivial, the main performance hit might arrise from a single assembly taking longer to load

            //   Assembly1 loading ................|register controls|register properties
            //    Assembly2 loading. waiting                         |registercontrols|...
            //     Assembly3 loading. waiting                                         |registercontrols|...

            var paralelismLimiter = new SemaphoreSlim(Environment.ProcessorCount);

            var controlIdsAssigned = Enumerable.Range(0, assemblies.Count).Select(_ => new TaskCompletionSource()).ToArray();

            var tasks = Enumerable.Range(0, assemblies.Count).Select(i => Task.Run(async () => {
                await paralelismLimiter.WaitAsync();
                try {
                    var controls = new List<Type>();
                    foreach (var type in assemblies[i].GetLoadableTypes())
                    {
                        if (type.IsClass && !type.ContainsGenericParameters && type.IsDefined(typeof(ContainsDotvvmPropertiesAttribute), true))
                        {
                            controls.Add(type);
                        }
                    }
                    // wait for the previous assembly to finish loading and assigning control IDs
                    if (i > 0)
                        await controlIdsAssigned[i - 1].Task;
                    var controlIds = new ushort[controls.Count];
                    DotvvmPropertyIdAssignment.RegisterTypes(CollectionsMarshal.AsSpan(controls), controlIds);
                    
                    // let the next assembly run
                    controlIdsAssigned[i].SetResult();

                    foreach (var type in controls)
                    {
                        InitType(type);
                    }
                }
                finally {
                    paralelismLimiter.Release();
                }

            })).ToArray();
            Task.WaitAll(tasks);
        }

        private static void InvokeStaticConstructorsOnAllControls(Assembly assembly)
        {
            foreach (var c in assembly.GetLoadableTypes())
            {
                if (!c.IsClass || c.ContainsGenericParameters)
                    continue;

                InitType(c);
            }
        }

        private static ConcurrentDictionary<Type, bool> typeInitSet = new();
        /// <summary> Ensures that the type is initialized - run .cctor and registers all properties/capabilities. </summary>
        internal static void InitType(Type type)
        {
            if (!type.IsDefined(typeof(ContainsDotvvmPropertiesAttribute), true))
                return;

            // just avoid mapping the type twice.
            // All actions following are idempotent, so it does not matter if we accidentally do it twice, but it's unnecessary waste of resources.
            if (typeInitSet.ContainsKey(type))
                return;

            if (type.BaseType != null)
                InitType(type.BaseType);

            RuntimeHelpers.RunClassConstructor(type.TypeHandle);

            RegisterCompositeControlProperties(type);
            RegisterCapabilitiesFromInterfaces(type);

            typeInitSet.TryAdd(type, true);
        }

        private static void RegisterCompositeControlProperties(Type type)
        {
            if (!type.IsAbstract && typeof(CompositeControl).IsAssignableFrom(type))
            {
                CompositeControl.RegisterProperties(type);
            }
        }

        private static void RegisterCapabilitiesFromInterfaces(Type type)
        {
            foreach (var capability in type.GetInterfaces())
            {
                if (capability.IsGenericType && capability.GetGenericTypeDefinition() == typeof(IObjectWithCapability<>))
                {
                    var capabilityType = capability.GetGenericArguments()[0];
                    // defined in generic type and contains generic arguments
                    // it will be probably registered in a derived control
                    if (capabilityType.ContainsGenericParameters)
                        continue;

                    if (DotvvmCapabilityProperty.GetCapabilities(type).Any(c => c.PropertyType == capabilityType))
                        continue;

                    DotvvmCapabilityProperty.RegisterCapability(type, capabilityType, capabilityAttributeProvider: new CustomAttributesProvider());
                }
            }
        }

        private Assembly[] GetAllRelevantAssemblies(string dotvvmAssembly)
        {
#if DotNetCore
            var assemblies = compiledAssemblyCache.GetAllAssemblies();
#else
            var loadedAssemblies = compiledAssemblyCache.GetAllAssemblies()
                .Where(a => ReferencesAssembly(a.GetReferencedAssemblies(), dotvvmAssembly));

            var visitedAssemblies = new HashSet<string>();

            // ReflectionUtils.GetAllAssemblies() in netframework returns only assemblies which have already been loaded into
            // the current AppDomain, to return all assemblies we traverse recursively all referenced Assemblies
            var assemblies = loadedAssemblies
                .SelectRecursively(a => a.GetReferencedAssemblies().Where(an => visitedAssemblies.Add(an.FullName)).Select(an => {
                    try
                    {
                        return Assembly.Load(an);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Unable to load assembly '{an.FullName}' referenced by '{a.FullName}'.", ex);
                    }
                }))
                .Where(a => ReferencesAssembly(a.GetReferencedAssemblies(), dotvvmAssembly))
                .Distinct()
                .ToArray();
#endif
            return assemblies;
        }

        /// <summary>
        /// After all DotvvmProperties have been registered, those marked with PropertyAliasAttribute can be resolved.
        /// </summary>
        private void ResolveAllPropertyAliases()
        {
            foreach (var alias in DotvvmProperty.AllAliases)
                DotvvmPropertyAlias.Resolve(alias);
        }

        /// <summary>
        /// After all DotvvmControls have been discovered, build a map of alternative names.
        /// </summary>
        private Dictionary<string, Type> BuildControlAliasesMap()
        {
            var mappings = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in configuration.Markup.Controls)
            {
                if (!string.IsNullOrEmpty(rule.TagName))
                {
                    // markup controls are not supported
                    continue;
                }

                // only for code-only controls
                var assembly = compiledAssemblyCache.GetAssembly(rule.Assembly!);
                if (assembly == null)
                {
                    throw new DotvvmConfigurationException($"The assembly {rule.Assembly} was not found!");
                }

                // find all control types
                var controlTypes = assembly.GetLoadableTypes()
                    .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
                    .Where(t => typeof(DotvvmBindableObject).IsAssignableFrom(t));

                // add mappings for primary names and aliases
                foreach (var controlType in controlTypes)
                {
                    if (controlType.GetCustomAttribute<ControlMarkupOptionsAttribute>() is { } markupOptions)
                    {
                        if (markupOptions.PrimaryName is {} primaryName)
                        {
                            mappings[$"{rule.TagPrefix}:{primaryName}"] = controlType;
                        }
                        if (markupOptions.AlternativeNames?.Any() == true)
                        {
                            foreach (var alternativeName in markupOptions.AlternativeNames)
                            {
                                var name = $"{rule.TagPrefix}:{alternativeName}";
                                if (mappings.TryGetValue(name, out _))
                                {
                                    throw new DotvvmCompilationException($"A conflicting primary name or alternative name {alternativeName} found at control {controlType.FullName}.");
                                }
                                mappings[name] = controlType;
                            }
                        }
                    }
                }
            }
            return mappings;
        }

        /// <summary>
        /// Resolves the control metadata for specified type.
        /// </summary>
        public override IControlResolverMetadata ResolveControl(ITypeDescriptor controlType)
        {
            var type = ((ResolvedTypeDescriptor)controlType).Type;
            return ResolveControl(new ControlType(type));
        }


        /// <summary>
        /// Finds the compiled control.
        /// </summary>
        protected override IControlType? FindCompiledControl(string tagPrefix, string tagName, string namespaceName, string assemblyName)
        {
            var type = controlNameMappings!.TryGetValue($"{tagPrefix}:{tagName}", out var mappedType)
                ? mappedType
                : compiledAssemblyCache.FindType($"{namespaceName}.{tagName}, {assemblyName}", ignoreCase: true);
            if (type == null)
            {
                // the control was not found
                return null;
            }

            return new ControlType(type);
        }


        /// <summary>
        /// Finds the markup control.
        /// </summary>
        protected override IControlType FindMarkupControl(string file)
        {
            var (descriptor, controlBuilder) = controlBuilderFactory.GetControlBuilder(file);
            return new ControlType(descriptor.ControlType, file, descriptor.DataContextType);
        }

        /// <summary>
        /// Gets the control metadata.
        /// </summary>
        public override IControlResolverMetadata BuildControlMetadata(IControlType type)
        {
            return new ControlResolverMetadata((ControlType)type);
        }

        public override IEnumerable<(string tagPrefix, string? tagName, IControlType type)> EnumerateControlTypes()
        {
            var markupControls = new HashSet<(string, string)>(); // don't report MarkupControl with @baseType twice

            foreach (var control in configuration.Markup.Controls)
            {
                if (!string.IsNullOrEmpty(control.Src))
                {
                    markupControls.Add((control.TagPrefix!, control.TagName!));
                    IControlType? markupControl = null;
                    try
                    {
                        markupControl = FindMarkupControl(control.Src);
                    }
                    catch { } // ignore the error, we should not crash here
                    if (markupControl != null)
                        yield return (control.TagPrefix!, control.TagName, markupControl);
                }
            }

            foreach (var assemblyGroup in configuration.Markup.Controls.Where(c => !string.IsNullOrEmpty(c.Assembly) && string.IsNullOrEmpty(c.Src)).GroupBy(c => c.Assembly!))
            {
                var assembly = compiledAssemblyCache.GetAssembly(assemblyGroup.Key);
                if (assembly is null)
                    continue;

                var namespaces = assemblyGroup.GroupBy(c => c.Namespace ?? "").ToDictionary(g => g.Key, g => g.First());
                foreach (var type in assembly.GetLoadableTypes())
                {
                    if (type.IsPublic && !type.IsAbstract &&
                        type.DeclaringType is null &&
                        typeof(DotvvmBindableObject).IsAssignableFrom(type) &&
                        namespaces.TryGetValue(type.Namespace ?? "", out var controlConfig))
                    {
                        if (!markupControls.Contains((controlConfig.TagPrefix!, type.Name)))
                            yield return (controlConfig.TagPrefix!, null, new ControlType(type));
                    }
                }
            }
        }

        protected override IPropertyDescriptor? FindGlobalPropertyOrGroup(string name, MappingMode requiredMode)
        {
            // try to find property
            var property = DotvvmProperty.ResolveProperty(name, caseSensitive: false);
            if (property != null)
            {
                return property;
            }

            // try to find property group
            return DotvvmPropertyGroup.ResolvePropertyGroup(name, caseSensitive: false, requiredMode);
        }
    }
}
