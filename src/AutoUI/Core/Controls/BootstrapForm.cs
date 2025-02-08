using System;
using System.Linq;
using DotVVM.AutoUI.Metadata;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Controls;

namespace DotVVM.AutoUI.Controls
{
    [ControlMarkupOptions(Precompile = ControlPrecompilationMode.InServerSideStyles)]
    public class BootstrapForm : AutoFormBase
    {
        public BootstrapForm(IServiceProvider services) : base(services)
        {
        }

        /// <summary>
        /// Gets or sets the CSS class that will be applied to the rendered label element.
        /// </summary>
        public string? LabelCssClass
        {
            get { return (string?)GetValue(LabelCssClassProperty); }
            set { SetValue(LabelCssClassProperty, value); }
        }
        public static readonly DotvvmProperty LabelCssClassProperty =
            DotvvmProperty.Register<string, BootstrapForm>(nameof(LabelCssClass), "form-label");

        /// <summary>
        /// Gets or sets the CSS class that will be applied to the root div element. Set this to 'form-group' if you are using Bootstrap 3 and 4.
        /// </summary>
        public string? FormGroupCssClass
        {
            get { return (string?)GetValue(FormGroupCssClassProperty); }
            set { SetValue(FormGroupCssClassProperty, value); }
        }
        public static readonly DotvvmProperty FormGroupCssClassProperty =
            DotvvmProperty.Register<string, BootstrapForm>(nameof(FormGroupCssClass), "mb-3");

        /// <summary>
        /// Gets or sets the CSS class that will be applied to the rendered control elements (TextBox and other input types, except for select, checkbox and radios).
        /// </summary>
        public string? FormControlCssClass
        {
            get { return (string?)GetValue(FormControlCssClassProperty); }
            set { SetValue(FormControlCssClassProperty, value); }
        }
        public static readonly DotvvmProperty FormControlCssClassProperty
            = DotvvmProperty.Register<string, BootstrapForm>(c => c.FormControlCssClass, "form-control");

        /// <summary>
        /// Gets or sets the CSS class that will be applied to the rendered select elements. Set this to 'form-control' if you are using Bootstrap 3 and 4.
        /// </summary>
        public string? FormSelectCssClass
        {
            get { return (string?)GetValue(FormSelectCssClassProperty); }
            set { SetValue(FormSelectCssClassProperty, value); }
        }
        public static readonly DotvvmProperty FormSelectCssClassProperty
            = DotvvmProperty.Register<string?, BootstrapForm>(c => c.FormSelectCssClass, "form-select");

        /// <summary>
        /// Gets or sets the CSS class that will be applied to the rendered div element when the form group contains checkboxes or radio buttons.
        /// </summary>
        public string? FormCheckCssClass
        {
            get { return (string?)GetValue(FormCheckCssClassProperty); }
            set { SetValue(FormCheckCssClassProperty, value); }
        }
        public static readonly DotvvmProperty FormCheckCssClassProperty
            = DotvvmProperty.Register<string?, BootstrapForm>(c => c.FormCheckCssClass, "form-check");

        /// <summary>
        /// Gets or sets the CSS class that will be applied on the label element of the CheckBox or RadioButton controls inside the form group.
        /// </summary>
        public string? FormCheckLabelCssClass
        {
            get { return (string?)GetValue(FormCheckLabelCssClassProperty); }
            set { SetValue(FormCheckLabelCssClassProperty, value); }
        }
        public static readonly DotvvmProperty FormCheckLabelCssClassProperty
            = DotvvmProperty.Register<string?, BootstrapForm>(c => c.FormCheckLabelCssClass, "form-check-label");

        /// <summary>
        /// Gets or sets the CSS class that will be applied on the input element of the CheckBox or RadioButton controls inside the form group.
        /// </summary>
        public string? FormCheckInputCssClass
        {
            get { return (string?)GetValue(FormCheckInputCssClassProperty); }
            set { SetValue(FormCheckInputCssClassProperty, value); }
        }
        public static readonly DotvvmProperty FormCheckInputCssClassProperty
            = DotvvmProperty.Register<string?, BootstrapForm>(c => c.FormCheckInputCssClass, "form-check-input");

        /// <summary>
        /// Gets or sets whether the generated control shall be wrapped in the div element. Set this to true for Bootstrap 3.
        /// </summary>
        public bool WrapControlInDiv
        {
            get { return (bool)GetValue(WrapControlInDivProperty)!; }
            set { SetValue(WrapControlInDivProperty, value); }
        }
        public static readonly DotvvmProperty WrapControlInDivProperty
            = DotvvmProperty.Register<bool, BootstrapForm>(c => c.WrapControlInDiv, false);



        public DotvvmControl GetContents(FieldProps props)
        {
            var context = CreateAutoUiContext();

            var resultPlaceholder = new PlaceHolder();

            // create the rows
            foreach (var property in GetPropertiesToDisplay(context, props.FieldSelector))
            {
                if (TryGetFieldTemplate(property, props) is { } field)
                {
                    resultPlaceholder.AppendChildren(field);
                    continue;
                }
                // create the row
                Label? labelElement;
                HtmlGenericControl controlElement;
                var formGroup = InitializeFormGroup(property, context, props, out labelElement, out controlElement);

                // create the validator
                InitializeValidation(controlElement, labelElement, property, context);

                SetFieldVisibility(formGroup, property, props, context);
                resultPlaceholder.Children.Add(formGroup);
            }
            return resultPlaceholder;
        }

        private void InitializeEditor(DotvvmControl editor, HtmlGenericControl controlElement)
        {
            if (editor.GetThisAndAllDescendants().Any(
                    d => d is CheckBox or RadioButton
                         || d.GetValue<bool>(RequiresFormCheckCssClassProperty)))
            {
                controlElement.AddCssClass(FormCheckCssClass);
            }
        }

        protected virtual HtmlGenericControl InitializeFormGroup(PropertyDisplayMetadata property, AutoUIContext autoUiContext, FieldProps props, out Label? labelElement, out HtmlGenericControl controlElement)
        {
            labelElement = InitializeControlLabel(property, autoUiContext, props);
            labelElement?.AddCssClass(LabelCssClass);

            var editor = CreateEditor(property, autoUiContext, props);
            if (!WrapControlInDiv && editor is HtmlGenericControl editorHtmlControl)
            {
                controlElement = editorHtmlControl;
            }
            else
            {
                controlElement = new HtmlGenericControl("div")
                    .AddCssClass(property.Styles?.FormControlContainerCssClass)
                    .AppendChildren(editor);
            }
            InitializeEditor(editor, controlElement);

            return
                new HtmlGenericControl("div")
                    .AddCssClasses(FormGroupCssClass, property.Styles?.FormRowCssClass)
                    .AppendChildren(labelElement, controlElement);
        }

        /// <summary>
        /// Indicates that when the AutoEditor control is used inside BootstrapForm, it should be wrapped in a div with a 'form-select' CSS class. This attached property is intended to be used when implementing custom FormEditorProviders.
        /// </summary>
        [AttachedProperty(typeof(bool))]
        public static readonly DotvvmProperty RequiresFormSelectCssClassProperty =
            DotvvmProperty.Register<bool, BootstrapForm>(() => RequiresFormSelectCssClassProperty, isValueInherited: false, defaultValue: false);

        /// <summary>
        /// Indicates that when the AutoEditor control is used inside BootstrapForm, it should be wrapped in a div with a 'form-control' CSS class. This attached property is intended to be used when implementing custom FormEditorProviders.
        /// </summary>
        [AttachedProperty(typeof(bool))]
        public static readonly DotvvmProperty RequiresFormControlCssClassProperty =
            DotvvmProperty.Register<bool, BootstrapForm>(() => RequiresFormControlCssClassProperty, isValueInherited: false, defaultValue: false);

        /// <summary>
        /// Indicates that when the AutoEditor control is used inside BootstrapForm, it should be wrapped in a div with a 'form-check' CSS class. This attached property is intended to be used when implementing custom FormEditorProviders.
        /// </summary>
        [AttachedProperty(typeof(bool))]
        public static readonly DotvvmProperty RequiresFormCheckCssClassProperty =
            DotvvmProperty.Register<bool, BootstrapForm>(() => RequiresFormCheckCssClassProperty, isValueInherited: false, defaultValue: false);

    }
}
