﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DotVVM.Analyzers {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("DotVVM.Analyzers.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method that declares that it should not be called on server is only meant to be invoked on client.
        /// </summary>
        internal static string ApiUsage_UnsupportedCallSite_Description {
            get {
                return ResourceManager.GetString("ApiUsage_UnsupportedCallSite_Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method &apos;{0}&apos; invocation is not supported on server.
        /// </summary>
        internal static string ApiUsage_UnsupportedCallSite_Message {
            get {
                return ResourceManager.GetString("ApiUsage_UnsupportedCallSite_Message", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unsupported call site.
        /// </summary>
        internal static string ApiUsage_UnsupportedCallSite_Title {
            get {
                return ResourceManager.GetString("ApiUsage_UnsupportedCallSite_Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fields are not supported in viewmodels. Use properties to save state of viewmodels instead..
        /// </summary>
        internal static string Serializability_DoNotUseFields_Description {
            get {
                return ResourceManager.GetString("Serializability_DoNotUseFields_Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fields are not supported in viewmodels.
        /// </summary>
        internal static string Serializability_DoNotUseFields_Message {
            get {
                return ResourceManager.GetString("Serializability_DoNotUseFields_Message", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The state must be represented using public properties.
        /// </summary>
        internal static string Serializability_DoNotUseFields_Title {
            get {
                return ResourceManager.GetString("Serializability_DoNotUseFields_Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unsupported property type in viewmodel..
        /// </summary>
        internal static string Serializability_NonSerializableType_Description {
            get {
                return ResourceManager.GetString("Serializability_NonSerializableType_Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property on path &apos;{0}&apos; is not serializable.
        /// </summary>
        internal static string Serializability_NonSerializableType_Message {
            get {
                return ResourceManager.GetString("Serializability_NonSerializableType_Message", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Properties must be serializable.
        /// </summary>
        internal static string Serializability_NonSerializableType_Title {
            get {
                return ResourceManager.GetString("Serializability_NonSerializableType_Title", resourceCulture);
            }
        }
    }
}
