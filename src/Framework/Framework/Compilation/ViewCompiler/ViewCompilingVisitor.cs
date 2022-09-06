using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Compilation.ViewCompiler
{
    public class ViewCompilingVisitor : ResolvedControlTreeVisitor
    {
        protected readonly DefaultViewCompilerCodeEmitter emitter;
        protected readonly IBindingCompiler bindingCompiler;

        protected int currentTemplateIndex;
        protected string? controlName;

        public Func<IControlBuilderFactory, IServiceProvider, DotvvmControl> BuildCompiledView { get; set; }

        public ViewCompilingVisitor(DefaultViewCompilerCodeEmitter emitter, IBindingCompiler bindingCompiler)
        {
            this.emitter = emitter;
            this.bindingCompiler = bindingCompiler;

            BuildCompiledView = (_, _) => throw new InvalidOperationException("The view cannot be built, bacause it hasn't been compiled yet.");
        }

        public override void VisitView(ResolvedTreeRoot view)
        {
            var isPageView = view.Metadata.Type == typeof(DotvvmView);

            if (isPageView)
            {
                emitter.ResultControlType = typeof(DotvvmView);
            }
            else
            {
                emitter.ResultControlType = view.Metadata.Type;
            }

            // build the statements
            emitter.PushNewMethod(emitter.EmitControlBuilderParameters());

            var pageName =
                isPageView ? emitter.EmitCreateObject(emitter.ResultControlType).Name :
                             EmitCreateControl(view.Metadata.Type, new object[0]).Name;
            pageName.NotNull();
            emitter.RegisterDotvvmProperties(pageName);

            emitter.EmitSetDotvvmProperty(pageName, Internal.UniqueIDProperty, pageName);
            emitter.EmitSetDotvvmProperty(pageName, Internal.MarkupFileNameProperty, view.Metadata.VirtualPath);
            // emitter.EmitSetDotvvmProperty(pageName, Internal.DataContextTypeProperty, emitter.EmitValue(view.DataContextTypeStack));

            if (typeof(DotvvmView).IsAssignableFrom(view.Metadata.Type))
                emitter.EmitSetProperty(pageName, nameof(DotvvmView.ViewModelType),
                    emitter.EmitValue(view.DataContextTypeStack.DataContextType));

            if (typeof(DotvvmView).IsAssignableFrom(view.Metadata.Type) ||
                typeof(DotvvmMarkupControl).IsAssignableFrom(view.Metadata.Type))
            {
                foreach (var directive in view.Directives)
                {
                    emitter.EmitAddDirective(pageName, directive.Key, directive.Value.First().Value);
                }
            }

            controlName = pageName;

            base.VisitView(view);

            emitter.CommitDotvvmProperties(pageName);

            emitter.EmitReturnClause(pageName);
            BuildCompiledView = emitter.PopMethod<Func<IControlBuilderFactory, IServiceProvider, DotvvmControl>>();
        }

        protected ParameterExpression EmitCreateControl(Type type, object[] arguments)
        {
            // if marked with [RequireDependencyInjection] attribute, invoke injected factory
            if (type.GetCustomAttribute(typeof(DependencyInjection.RequireDependencyInjectionAttribute)) is DependencyInjection.RequireDependencyInjectionAttribute requireDiAttr)
                return emitter.EmitCustomInjectionFactoryInvocation(requireDiAttr.FactoryType, type);
            // if matching ctor exists, invoke it directly
            else if (type.GetConstructors().FirstOrDefault(ctor =>
                ctor.GetParameters().Count(p => !p.HasDefaultValue) <= arguments.Length &&
                ctor.GetParameters().Length >= arguments.Length &&
                ctor.GetParameters().Zip(arguments,
                        (p, a) => TypeConversion.ImplicitConversion(Expression.Constant(a), p.ParameterType))
                    .All(a => a != null)) is ConstructorInfo constructor)
            {
                var optionalArguments =
                    constructor.GetParameters().Skip(arguments.Length)
                    .Select(a =>
                        a.ParameterType == typeof(bool) && a.Name == "allowImplicitLifecycleRequirements" ? false :
                        a.DefaultValue
                    );
                return emitter.EmitCreateObject(constructor, arguments.Concat(optionalArguments).ToArray());
            }
            // otherwise invoke DI factory
            else
            {
                return emitter.EmitInjectionFactoryInvocation(type, arguments);
            }
        }


        /// <summary>
        /// Processes the node.
        /// </summary>
        public override void VisitControl(ResolvedControl node)
        {
            var parentName = controlName.NotNull();
            var localControlName = controlName = CreateControl(node);

            base.VisitControl(node);

            Debug.Assert(localControlName == controlName);

            emitter.CommitDotvvmProperties(controlName);

            emitter.EmitAddChildControl(parentName, controlName);
            controlName = parentName;
        }

        private void SetProperty(string controlName, DotvvmProperty property, Expression value)
        {
            // set special properties as fields
            if (property == LifecycleRequirementsAssigningVisitor.CompileTimeLifecycleRequirementsProperty)
                emitter.EmitSetProperty(controlName, nameof(DotvvmControl.LifecycleRequirements), value);
            if (property is CompileTimeOnlyDotvvmProperty)
            {
                // just don't set compile time only properties
            }
            else emitter.EmitSetDotvvmProperty(controlName, property, value);
        }

        private void SetPropertyValue(string controlName, DotvvmProperty property, object? value)
            => SetProperty(controlName, property, emitter.EmitValue(value));

        public override void VisitPropertyValue(ResolvedPropertyValue propertyValue)
        {
            SetPropertyValue(controlName.NotNull(), propertyValue.Property, propertyValue.Value);
            base.VisitPropertyValue(propertyValue);
        }

        public override void VisitPropertyBinding(ResolvedPropertyBinding propertyBinding)
        {
            SetProperty(controlName.NotNull(), propertyBinding.Property, ProcessBinding(propertyBinding.Binding));
            base.VisitPropertyBinding(propertyBinding);
        }

        public override void VisitPropertyControl(ResolvedPropertyControl propertyControl)
        {
            if (propertyControl.Property is CompileTimeOnlyDotvvmProperty)
                return;

            var control = propertyControl.Control.NotNull();
            var parentName = controlName.NotNull();
            controlName = CreateControl(control);
            // compile control content
            base.VisitControl(control);
            emitter.CommitDotvvmProperties(controlName);
            emitter.EmitSetProperty(controlName, nameof(DotvvmControl.Parent), emitter.GetParameterOrVariable(parentName));
            // set the property
            SetProperty(parentName, propertyControl.Property, emitter.GetParameterOrVariable(controlName));
            controlName = parentName;
        }

        public override void VisitPropertyControlCollection(ResolvedPropertyControlCollection propertyControlCollection)
        {
            if (propertyControlCollection.Property is CompileTimeOnlyDotvvmProperty)
                return;

            var parentName = controlName.NotNull();
            var collectionName = emitter.EmitEnsureCollectionInitialized(parentName, propertyControlCollection.Property).Name.NotNull();

            foreach (var control in propertyControlCollection.Controls)
            {
                controlName = CreateControl(control);

                // compile control content
                base.VisitControl(control);

                emitter.CommitDotvvmProperties(controlName);

                // add to collection in property
                emitter.EmitSetProperty(controlName, nameof(DotvvmControl.Parent), emitter.GetParameterOrVariable(parentName));
                emitter.EmitAddCollectionItem(collectionName, controlName);
            }
            controlName = parentName;
        }

        public override void VisitPropertyTemplate(ResolvedPropertyTemplate propertyTemplate)
        {
            if (propertyTemplate.Property is CompileTimeOnlyDotvvmProperty) { return; }

            var parentName = controlName.NotNull();

            //compiledDelegate = (controlBuilderFactory, sercices, templateContainer) => {
            //   ...the template control building statements go here
            //}

            emitter.PushNewMethod(emitter.EmitControlBuilderParameters().Concat(new[] { emitter.EmitParameter("templateContainer", typeof(DotvvmControl)) }).ToArray());

            controlName = "templateContainer";

            base.VisitPropertyTemplate(propertyTemplate);

            // add return void, otherwise it may fail on deprecated .NET framework
            emitter.EmitStatement(Expression.Default(typeof(void)));

            var compiledDelegate = emitter.PopMethod<Action<IControlBuilderFactory, IServiceProvider, DotvvmControl>>();
            controlName = parentName;

            //[parent].[propertyTemplate.Property] = new DelegateTemplate { BuildContentBody = compiledDelegate }

            var delegateTemplateValue = emitter.EmitValue(new DelegateTemplate { BuildContentBody = compiledDelegate });

            SetProperty(controlName, propertyTemplate.Property, delegateTemplateValue);
        }

        /// <summary>
        /// Processes the HTML element that represents a new object.
        /// </summary>
        protected string CreateControl(ResolvedControl control)
        {
            string name;

            if (control.Metadata.VirtualPath == null)
            {
                var arguments = control.ConstructorParameters ?? Array.Empty<object>();
                // compiled control
                name = EmitCreateControl(control.Metadata.Type, arguments).Name.NotNull();
            }
            else
            {
                // markup control
                name = emitter.EmitInvokeControlBuilder(control.Metadata.Type, control.Metadata.VirtualPath).Name.NotNull();
            }

            emitter.RegisterDotvvmProperties(name);
            // RawLiterals don't need these helper properties unless in root
            if (control.Metadata.Type != typeof(RawLiteral) || control.Parent is ResolvedTreeRoot)
            {
                // set unique id
                emitter.EmitSetDotvvmProperty(name, Internal.UniqueIDProperty, name);

                if (control.DothtmlNode != null && control.DothtmlNode.Tokens.Count > 0)
                {
                    // set line number
                    emitter.EmitSetDotvvmProperty(name, Internal.MarkupLineNumberProperty, control.DothtmlNode.Tokens.First().LineNumber);
                }
            }

            return name;
        }

        /// <summary>
        /// Emits binding constructor and returns variable name
        /// </summary>
        protected Expression ProcessBinding(ResolvedBinding binding)
        {
            return bindingCompiler.EmitCreateBinding(emitter, binding);
        }
    }
}
