using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Compilation;
using System.Reflection;
using System.ComponentModel;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Javascript;
using System.Text.Json.Serialization;
using DotVVM.Framework.Compilation.Parser.Dothtml;

namespace DotVVM.Framework.Configuration
{
    public sealed class DotvvmMarkupConfiguration
    {
        /// <summary>
        /// Gets the registered control namespaces.
        /// </summary>
        [JsonPropertyName("controls")]
        public IList<DotvvmControlConfiguration> Controls => _controls;
        private readonly FreezableList<DotvvmControlConfiguration> _controls;

        /// <summary>
        /// Gets or sets the list of referenced assemblies.
        /// </summary>
        [JsonPropertyName("assemblies")]
        public IList<string> Assemblies => _assemblies;
        private readonly FreezableList<string> _assemblies;

        /// <summary>
        /// Gets a list of HTML attribute transforms.
        /// </summary>
        //[JsonPropertyName("htmlAttributeTransforms")]
        [JsonIgnore]
        public IDictionary<HtmlTagAttributePair, HtmlAttributeTransformConfiguration> HtmlAttributeTransforms => _htmlAttributeTransforms;
        private readonly FreezableDictionary<HtmlTagAttributePair, HtmlAttributeTransformConfiguration> _htmlAttributeTransforms;

        /// <summary>
        /// Gets a list of HTML attribute transforms.
        /// </summary>
        [JsonPropertyName("defaultDirectives")]
        public IDictionary<string, string> DefaultDirectives => _defaultDirectives;
        private readonly FreezableDictionary<string, string> _defaultDirectives;
        /// <summary>
        /// Gets or sets list of namespaces imported in bindings
        /// </summary>
        [JsonPropertyName("importedNamespaces")]
        public IList<NamespaceImport> ImportedNamespaces
        {
            get => _importedNamespaces;
            set { ThrowIfFrozen(); _importedNamespaces = value; }
        }
        private IList<NamespaceImport> _importedNamespaces = new FreezableList<NamespaceImport> {
            new NamespaceImport("DotVVM.Framework.Binding.HelperNamespace"),
            new NamespaceImport("System.Linq"),
        };

        [JsonIgnore]
        public JavascriptTranslatorConfiguration JavascriptTranslator => _javascriptTranslator.Value;
        private readonly Lazy<JavascriptTranslatorConfiguration> _javascriptTranslator;


        [JsonPropertyName("defaultExtensionParameters")]
        public IList<BindingExtensionParameter> DefaultExtensionParameters
        {
            get => _defaultExtensionParameters;
            set { ThrowIfFrozen(); _defaultExtensionParameters = value; }
        }
        private IList<BindingExtensionParameter> _defaultExtensionParameters = new FreezableList<BindingExtensionParameter>();

        public ViewCompilationConfiguration ViewCompilation { get; private set; } = new ViewCompilationConfiguration();

        /// <summary> List of HTML elements which content is not parsed as [dot]html, but streated as raw text until the end tag. By default it is <c>script</c> and <c>style</c> tags in addition to DotVVM <c>dot:InlineScript</c>. The property is meant primarily as compatibility option, as it may be ignored by tooling. </summary>
        [JsonPropertyName("rawTextElements")]
        public IList<string> RawTextElements
        {
            get => _rawTextElements;
            set { ThrowIfFrozen(); _rawTextElements = [..value]; }
        }
        private IList<string> _rawTextElements = new FreezableList<string>(DotvvmSyntaxConfiguration.Default.RawTextElements);


        public void AddServiceImport(string identifier, Type type)
        {
            ThrowIfFrozen();
            DefaultExtensionParameters.Add(new InjectedServiceExtensionParameter(identifier, new ResolvedTypeDescriptor(type)));
        }

        public DotvvmMarkupConfiguration(): this(null) { }
        public DotvvmMarkupConfiguration(Lazy<JavascriptTranslatorConfiguration>? javascriptConfig)
        {
            this._javascriptTranslator = javascriptConfig ?? new Lazy<JavascriptTranslatorConfiguration>(() => new JavascriptTranslatorConfiguration());
            this._controls = new FreezableList<DotvvmControlConfiguration>();
            this._assemblies = new FreezableList<string>();
            this._defaultDirectives = new FreezableDictionary<string, string>();
            this._htmlAttributeTransforms = new FreezableDictionary<HtmlTagAttributePair, HtmlAttributeTransformConfiguration>()
            {
                {
                    new HtmlTagAttributePair { TagName = "a", AttributeName = "href" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "link", AttributeName = "href" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "img", AttributeName = "src" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "iframe", AttributeName = "src" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "script", AttributeName = "src" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "meta", AttributeName = "content" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
            };
        }

        /// <summary>
        /// Adds the assembly to the list of required assemblies.
        /// </summary>
        public void AddAssembly(string assemblyName)
        {
            if (assemblyName is null) throw new ArgumentNullException(nameof(assemblyName));
            ThrowIfFrozen();
            if (!Assemblies.Contains(assemblyName))
            {
                Assemblies.Add(assemblyName);
            }
        }

        /// <summary> Adds the assembly to the list of required assemblies. </summary>
        public void AddAssembly(Assembly assembly)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            if (assembly.FullName is null) throw new ArgumentException("Assembly does not have a FullName", nameof(assembly));
            AddAssembly(assembly.FullName);
        }


        /// <summary>
        /// Registers markup control
        /// </summary>
        public void AddMarkupControl(string tagPrefix, string tagName, string src)
        {
            ThrowIfFrozen();
            Controls.Add(new DotvvmControlConfiguration { TagPrefix = tagPrefix, TagName = tagName, Src = src });
        }

        /// <summary>
        /// Registers code controls in the specified namespace from the specified assembly
        /// </summary>
        public void AddCodeControls(string tagPrefix, string namespaceName, string assembly)
        {
            ThrowIfFrozen();
            Controls.Add(new DotvvmControlConfiguration { TagPrefix = tagPrefix, Namespace = namespaceName, Assembly = assembly });
            AddAssembly(assembly);
        }

        /// <summary>
        /// Registers code controls from the same namespace and assembly as exampleControl
        /// </summary>
        public void AddCodeControls(string tagPrefix, Type exampleControl)
        {
            ThrowIfFrozen();
            AddAssembly(exampleControl.Assembly.FullName!);
            Controls.Add(new DotvvmControlConfiguration { TagPrefix = tagPrefix, Namespace = exampleControl.Namespace, Assembly = exampleControl.Assembly.FullName });
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use AddCodeControls instead.")]
        public void AddCodeControl(string tagPrefix, string namespaceName, string assembly) => AddCodeControls(tagPrefix, namespaceName, assembly);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use AddCodeControls instead.")]
        public void AddCodeControl(string tagPrefix, Type exampleControl) => AddCodeControls(tagPrefix, exampleControl);

        private bool isFrozen = false;

        private void ThrowIfFrozen()
        {
            if (isFrozen)
                throw FreezableUtils.Error(nameof(DotvvmMarkupConfiguration));
        }
        public void Freeze()
        {
            this.isFrozen = true;

            ViewCompilation.Freeze();
            _controls.Freeze();
            
            foreach (var c in this.Controls)
                c.Freeze();
            _assemblies.Freeze();
            _htmlAttributeTransforms.Freeze();
            foreach (var t in this.HtmlAttributeTransforms)
                t.Value.Freeze();
            _defaultDirectives.Freeze();
            FreezableList.Freeze(ref _importedNamespaces);
            JavascriptTranslator.Freeze();
            FreezableList.Freeze(ref _defaultExtensionParameters);
            FreezableList.Freeze(ref _rawTextElements);
        }
    }
}
