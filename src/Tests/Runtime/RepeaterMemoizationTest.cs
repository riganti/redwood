using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Io;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotVVM.Framework.Tests.Runtime
{
    [TestClass]
    public class RepeaterMemoizationTest: DotvvmControlTestBase
    {
        (Repeater, TestDotvvmRequestContext) Init<T>(T viewModel, ITemplate template)
            where T: IEnumerable
        {
            var dataContextStack = DataContextStack.Create(viewModel.GetType());
            var repeater = new Repeater() {
                DataSource = ValueBindingExpression.CreateBinding(BindingService, h => (T)h[0], dataContextStack),
                ItemTemplate = template
            };
            repeater.SetValue(RenderSettings.ModeProperty, RenderMode.Client);

            var context = CreateContext(viewModel);

            return (repeater, context);
        }

        [DataTestMethod]
        [DataRow(DotvvmRequestType.Navigate, RenderMode.Client)]
        [DataRow(DotvvmRequestType.Navigate, RenderMode.Server)]
        [DataRow(DotvvmRequestType.SpaNavigate, RenderMode.Client)]
        [DataRow(DotvvmRequestType.SpaNavigate, RenderMode.Server)]
        [DataRow(DotvvmRequestType.Command, RenderMode.Client)]
        [DataRow(DotvvmRequestType.Command, RenderMode.Server)]
        public void Repeater_TemplateInitializedOnce(DotvvmRequestType requestType, RenderMode renderMode)
        {
            var templateInits = new List<string>();
            var (repeater, context) = Init(new[] { "ROW 1", "ROW 2", "ROW 3" }, new DelegateTemplate((sp, body) => {

                body.AppendChildren(new Literal("test"));
                templateInits.Add((string)body.DataContext);
            }));
            repeater.SetValue(RenderSettings.ModeProperty, renderMode);
            context.RequestType = requestType;
            var html = InvokeLifecycleAndRender(repeater, context);

            Console.WriteLine("Template inits: " + string.Join(", ", templateInits.Select(i => i ?? "<null>")));

            if (renderMode == RenderMode.Client)
            {
                XAssert.Equal(["ROW 1", "ROW 2", "ROW 3", null], templateInits);
            }
            else
            {
                XAssert.Equal(["ROW 1", "ROW 2", "ROW 3"], templateInits);
            }
        }

        [DataTestMethod]
        [DataRow(DotvvmRequestType.Navigate, RenderMode.Client)]
        [DataRow(DotvvmRequestType.Navigate, RenderMode.Server)]
        [DataRow(DotvvmRequestType.SpaNavigate, RenderMode.Client)]
        [DataRow(DotvvmRequestType.SpaNavigate, RenderMode.Server)]
        [DataRow(DotvvmRequestType.Command, RenderMode.Client)]
        [DataRow(DotvvmRequestType.Command, RenderMode.Server)]
        public void Repeater_ViewModelChange(DotvvmRequestType requestType, RenderMode renderMode)
        {
            var templateInits = new List<string>();
            var viewModel = new[] { "ROW 1", "ROW 2", "ROW 3" };
            var (repeater, context) = Init(viewModel, new DelegateTemplate((sp, body) => {

                body.AppendChildren(new Literal("test"));
                templateInits.Add((string)body.DataContext);
            }));
            context.RequestType = requestType;
            repeater.SetValue(RenderSettings.ModeProperty, renderMode);

            var view = new DotvvmView();
            view.Children.Add(repeater);
            view.SetValue(Internal.RequestContextProperty, context);
            view.DataContext = context.ViewModel;

            // Run Load event

            DotvvmControlCollection.InvokePageLifeCycleEventRecursive(view, LifeCycleEventType.Load, context);
            Console.WriteLine("Template inits (Load): " + string.Join(", ", templateInits.Select(i => i ?? "<null>")));

            if (requestType == DotvvmRequestType.Command)
                XAssert.Equal(["ROW 1", "ROW 2", "ROW 3"], templateInits);
            else
                XAssert.Equal([], templateInits);

            // Continue with PreRender and Render after shuffling the view model

            view.DataContext = context.ViewModel = viewModel = [ viewModel[1], viewModel[2], "ROW 4", viewModel[1] ];

            var html = InvokeLifecycleAndRender(view, context);

            Console.WriteLine("Template inits (Render): " + string.Join(", ", templateInits.Select(i => i ?? "<null>")));

            string[] nullIfClient = renderMode == RenderMode.Client ? [ null ] : [];

            if (requestType == DotvvmRequestType.Command)
            {
                XAssert.Equal(["ROW 1", "ROW 2", "ROW 3", /* Load end */ "ROW 4", "ROW 2", ..nullIfClient], templateInits);
            }
            else
            {
                XAssert.Equal(["ROW 2", "ROW 3", "ROW 4", "ROW 2", ..nullIfClient], templateInits);
            }
        }

        [DataTestMethod]
        [DataRow(DotvvmRequestType.Navigate, RenderMode.Client)]
        [DataRow(DotvvmRequestType.Navigate, RenderMode.Server)]
        [DataRow(DotvvmRequestType.SpaNavigate, RenderMode.Client)]
        [DataRow(DotvvmRequestType.SpaNavigate, RenderMode.Server)]
        [DataRow(DotvvmRequestType.Command, RenderMode.Client)]
        [DataRow(DotvvmRequestType.Command, RenderMode.Server)]
        public void Repeater_IsCollectible(DotvvmRequestType requestType, RenderMode renderMode)
        {
            var (repeaterRef, contextRef, viewModelRef) = RunLifecycle(requestType, renderMode);

            for (int i = 0; i < 100; i++) // single collection is enough normally, but just to be sure it's not flaky on CI...
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);

                Console.WriteLine($"GC Collect #{i} ({repeaterRef.IsAlive}, {contextRef.IsAlive}, {viewModelRef.IsAlive})");

                if (!repeaterRef.IsAlive || !contextRef.IsAlive || !viewModelRef.IsAlive)
                {
                    break;
                }
            }

            XAssert.Equal((false, false, false), (repeaterRef.IsAlive, contextRef.IsAlive, viewModelRef.IsAlive));

            (WeakReference, WeakReference, WeakReference) RunLifecycle(DotvvmRequestType requestType, RenderMode renderMode)
            {
                var (repeater, context) = Init(new[] { "ROW 1", "ROW 2", "ROW 3" }, new DelegateTemplate((sp, body) => {
                    body.AppendChildren(new Literal("test"));
                }));
                repeater.SetValue(RenderSettings.ModeProperty, renderMode);
                context.RequestType = requestType;
                var html = InvokeLifecycleAndRender(repeater, context);

                return (new WeakReference(repeater), new WeakReference(context), new WeakReference(context.ViewModel));
            }
        }
    }
}
