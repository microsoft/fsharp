// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.Editors.PropertyPages 
{
    using EnvDTE;
    using Microsoft.Win32;
    using Microsoft.VisualStudio.Shell.Interop;
    using System;
    using System.Collections;
    using System.Collections.Generic;   
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Reflection;
    using System.Runtime.InteropServices;   
    using System.Runtime.Serialization.Formatters;
    using System.Windows.Forms.ComponentModel.Com2Interop;
        
    using System.Windows.Forms.Design;

    public sealed class AutomationExtenderManager {
        private static string extenderPropName = "ExtenderCATID";
        
        private ObjectExtenders extensionMgr = null;


        internal AutomationExtenderManager(ObjectExtenders oe) {
            this.extensionMgr = oe;
        }
        
        public static AutomationExtenderManager GetAutomationExtenderManager(IServiceProvider sp) {
            return new AutomationExtenderManager((ObjectExtenders)sp.GetService(typeof(ObjectExtenders)));
        }

        public bool GetAllExtenders(object[] selectedObjects, ArrayList allExtenders)
        {
            Hashtable[] extenderList = null;
            bool nullListFound = false;
            return GetAllExtenders(selectedObjects, allExtenders, ref extenderList, ref nullListFound);
        }

        public bool GetAllExtenders(object[] selectedObjects, ArrayList allExtenders, ref Hashtable[] extenderList, ref bool nullListFound)
        {
            // 1 : handle intrinsic extensions
            string catID = null;

            // do the intersection of intrinsic extensions
            for (int i = 0; i < selectedObjects.Length; i++) {
                string id = GetCatID(selectedObjects[i]);
               
                // make sure this value is equal to
                // all the others.
                //
                if (catID == null && i == 0) {
                    catID = id;
                }
                else if (catID == null || !catID.Equals(id)) {
                    catID = null;
                    break;
                }
            }

            // okay, now we've got a common catID, get the names of each extender for each object for it
            // here is also where we'll pickup the contextual extenders
            //

            // ask the extension manager for any contextual IDs
            string[] contextualCATIDs = GetContextualCatIDs(extensionMgr);

            // if we didn't get an intersection and we didn't get any contextual
            // extenders, quit!
            //
            if ((contextualCATIDs == null || contextualCATIDs.Length == 0) && catID == null) {
                return false;
            }

            extenderList = new Hashtable[selectedObjects.Length];
            int firstWithItems = -1;

            // right, here we go; build up the mappings from extender names to extensions
            //
            for (int i = 0; i < selectedObjects.Length;i++) {
            
                // NOTE: this doesn't actually replace extenderList[i], it
                // just adds items to it and returns a new one if one didn't
                // get passed in.  So it is possible to get extenders from both
                // the catID and the contextualCATID.
                //
                if (catID != null) {
                    extenderList[i] = GetExtenders(extensionMgr, catID, selectedObjects[i], extenderList[i]);
                }

                if (contextualCATIDs != null) {
                    for (int c = 0; c < contextualCATIDs.Length; c++) {
                        extenderList[i] = GetExtenders(extensionMgr, contextualCATIDs[c], selectedObjects[i], extenderList[i]);
                    }
                }

                // did we create items for the first time?
                //
                if (firstWithItems == -1 && extenderList[i] != null && extenderList[i].Count > 0) {
                    firstWithItems = i;
                }
                
                // make sure the first one has items, otherwise
                // we can't do an intersection, so quit
                //
                if (i == 0 && firstWithItems == -1) {
                     break;
                }
            }

            // the very first item must have extenders or we can skip the merge too
            //
            if (firstWithItems == 0) {

                // now we've gotta merge the extender names to get the common ones...
                // so we just walk through the list of the first one and see if all
                // the others have the values...
                string[] hashKeys = new string[extenderList[0].Keys.Count];
                extenderList[0].Keys.CopyTo(hashKeys, 0);
                nullListFound = false;

                // walk through all the others looking for the common items.
                for (int n = 0; !nullListFound && n < hashKeys.Length; n++)
                {
                    bool found = true;
                    string name = (string)hashKeys[n];

                    // add it to the total list
                    allExtenders.Add(extenderList[0][name]);

                    // walk through all the extender lists looking for 
                    // and item of this name.  If one doesn't have it,
                    // we remove it from all the lists, but continue
                    // to walk through because we need to 
                    // add all of the extenders to the global list
                    // for the IFilterProperties walk below
                    //
                    for (int i = 1; i < extenderList.Length; i++) {
                        // if we find a null list, quit
                        if (extenderList[i] == null || extenderList[i].Count == 0) {
                            nullListFound = true;
                            break;
                        }

                        object extender = extenderList[i][name];

                        // do we have this item?
                        if (found) {
                            found &= (extender != null);
                        }

                        // add it to the total list
                        allExtenders.Add(extender);

                        // if we don't find it, remove it from this list
                        //
                        if (!found) {
                            // If this item is in the
                            // middle of the list, do we need to go back
                            // through and remove it from the prior lists?
                            //
                            extenderList[i].Remove(name);
                        }
                    }

                    // if we don't find it, remove it from the list
                    //
                    if (!found) {
                        object extenderItem = extenderList[0][name];
                        extenderList[0].Remove(name);
                    }
                 
                }
            }
            return true;
        }

        public object[] GetExtendedObjects(object[] selectedObjects) { // INTENTIONAL CHANGE from VSIP version of AutomationExtenderManager.cs: internal to public
            if (extensionMgr == null || selectedObjects == null || selectedObjects.Length == 0) {
                return selectedObjects;
            }

            Hashtable[] extenderList = null;
            ArrayList allExtenders = new ArrayList();
            bool fail = false;
            if (!GetAllExtenders(selectedObjects, allExtenders, ref extenderList, ref fail))
                return selectedObjects;

            // do the IFilterProperties stuff
            // here we walk through all the items and apply I filter properites...
            // 1: do the used items
            // 2: do the discarded items.
            // 
            // this makes me queasy just thinking about it.
            //
            IEnumerator extEnum = allExtenders.GetEnumerator();
            Hashtable modifiedObjects = new Hashtable();

            while (extEnum.MoveNext())
            {
                object extender = extEnum.Current;
                IFilterProperties dteFilter = extender as IFilterProperties;
                AutomationExtMgr_IFilterProperties privateFilter = extender as AutomationExtMgr_IFilterProperties;

                Debug.Assert(privateFilter == null || dteFilter != null, "How did we get a private filter but no public DTE filter?");

                if (dteFilter != null)
                {
                    vsFilterProperties filter;

                    // ugh, walk through all the properties of all the objects
                    // and see if this guy would like to filter them
                    // icky and n^2, but that's the spec...
                    //
                    for (int x = 0; x < selectedObjects.Length; x++)
                    {
                        PropertyDescriptorCollection props = TypeDescriptor.GetProperties(selectedObjects[x], new Attribute[0]); // INTENTIONAL CHANGE from VSIP version of AutomationExtenderManager.cs: passing in empty Attribute array instead of {BrowsableAttribute.Yes} - see comments at beginning
                        props.Sort();
                        for (int p = 0; p < props.Count; p++)
                        {
                            filter = vsFilterProperties.vsFilterPropertiesNone;
                            if (privateFilter != null)
                            {
                                if (VSConstants.S_OK != privateFilter.IsPropertyHidden(props[p].Name, out filter))
                                {
                                    filter = vsFilterProperties.vsFilterPropertiesNone;
                                }
                            }
                            else
                            {
                                filter = dteFilter.IsPropertyHidden(props[p].Name);
                            }

                            FilteredObjectWrapper filteredObject = (FilteredObjectWrapper)modifiedObjects[selectedObjects[x]];
                            if (filteredObject == null)
                            {
                                filteredObject = new FilteredObjectWrapper(selectedObjects[x]);
                                modifiedObjects[selectedObjects[x]] = filteredObject;
                            }

                            switch (filter)
                            {
                                case vsFilterProperties.vsFilterPropertiesAll:
                                    filteredObject.FilterProperty(props[p], BrowsableAttribute.No);
                                    break;
                                case vsFilterProperties.vsFilterPropertiesSet:
                                    filteredObject.FilterProperty(props[p], ReadOnlyAttribute.Yes);
                                    break;
                            }
                        }
                    }
                }
            }

            // finally, wrap any extended objects in extender proxies for browsing...
            //
            bool applyExtenders = extenderList[0].Count > 0 && !fail;
            if (modifiedObjects.Count > 0 || applyExtenders)
            {


                // create the return array
                selectedObjects = (object[])selectedObjects.Clone();
                Dictionary<object, ExtendedObjectWrapper> objectsWrapperCollection = new Dictionary<object, ExtendedObjectWrapper>();
                for (int i = 0; i < selectedObjects.Length; i++)
                {
                    object originalObj = selectedObjects[i];
                    object obj = modifiedObjects[originalObj];
                    if (obj == null)
                    {
                        if (applyExtenders)
                        {
                            obj = originalObj;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    selectedObjects[i] = new ExtendedObjectWrapper(obj, extenderList[i], objectsWrapperCollection, originalObj);
                }
            }

            // phewwwy, done!
            return selectedObjects;
        }

        private static string GetCatID(object component) {
            // 1 : handle intrinsic extensions
            string catID = null;

            if (Marshal.IsComObject(component)) {
                bool success = false;
                Type descriptorType = Type.GetType("System.Windows.Forms.ComponentModel.Com2Interop.ComNativeDescriptor, " + typeof(System.Windows.Forms.Form).Assembly.FullName);
                Debug.Assert(descriptorType != null, "No comnative descriptor; we can't get native property values");
                if (descriptorType != null) {
                    MethodInfo getPropertyValue = descriptorType.GetMethod("GetNativePropertyValue", BindingFlags.Static | BindingFlags.Public);
                    Debug.Assert(getPropertyValue != null, "Unable to find GetNativePropertyValue on ComNativeDescriptor");
                    if (getPropertyValue != null)
                    {
                        object[] args = new object[] {component, extenderPropName, success};
                        catID = (string)getPropertyValue.Invoke(null, args);
                        success = (bool)args[2];
                    }
                }
            }
            else {
                PropertyInfo propCatID = TypeDescriptor.GetReflectionType(component).GetProperty(extenderPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
                if (propCatID != null) {
                    object[] tempIndex = null;
                    catID = (string)propCatID.GetValue(component, tempIndex);
                }
            }

            if (catID != null && catID.Length > 0) {
                try {
                    // is this a vaild catID string?
                    Guid g = new Guid(catID);
                }
                catch (FormatException) {
                    Debug.Fail("'" + catID + "' is not a valid CatID (GUID) string");
                    catID = null;
                }
            }
            else {
                catID = null;
            }
            
            return catID;
        }
        
        private static string[] GetContextualCatIDs(ObjectExtenders extensionMgr) {
            string[] catIds = null;

            try {
                Object obj = extensionMgr.GetContextualExtenderCATIDs();

#if DEBUG
                string vType = obj.GetType().FullName;
#endif

                if (obj.GetType().IsArray) {
                    Array catIDArray = (Array)obj;
                    if (typeof(string).IsAssignableFrom(catIDArray.GetType().GetElementType())) {
                        catIds = (string[])catIDArray;
                    }
                }
            }
            catch {
            }

            return catIds;
        }

        private static string[] GetExtenderNames(ObjectExtenders extensionMgr, string catID, object extendee) {

            if (extensionMgr == null) {
                return new string[0];
            }

            try {
                Object obj = extensionMgr.GetExtenderNames(catID, extendee);

                if (obj == null || Convert.IsDBNull(obj)) {
                    return new string[0];
                }

                if (obj is Array && typeof(string).IsAssignableFrom(obj.GetType().GetElementType())) {
                    return(string[])((Array)obj);
                }
            }
            catch  {
                return new string[0];
            }

            return new string[0];
        }

        private static Hashtable GetExtenders(ObjectExtenders extensionMgr, string catID, object extendee, Hashtable ht) {
            if (extensionMgr == null) {
                return null;
            }

            if (ht == null) {
                ht = new Hashtable();
            }

            object pDisp = extendee;

            // generate the extender name list.
            string[] extenderNames = GetExtenderNames(extensionMgr, catID, pDisp);

            for (int i = 0; i < extenderNames.Length; i++) {
                try {
                    object pDispExtender = extensionMgr.GetExtender(catID, extenderNames[i], pDisp);

                    if (pDispExtender != null) {
                        // we've got one, so add it to our list
                        ht.Add(extenderNames[i], pDispExtender);
                    }
                }
                catch {
                }
            }
            return ht;
        }
        
    }
    
    [ComImport, Guid("aade1f59-6ace-43d1-8fca-42af3a5c4f3c"),InterfaceTypeAttribute(ComInterfaceType.InterfaceIsDual)]
    internal interface  AutomationExtMgr_IFilterProperties {
   
           [return: System.Runtime.InteropServices.MarshalAs(UnmanagedType.I4)]
           [PreserveSig]                             
           int IsPropertyHidden(string name, [Out]out vsFilterProperties propertyHidden);
   
     }


    internal class ExtendedObjectWrapper : ICustomTypeDescriptor {
    #if DEBUG
        private static int count = 0;
        private int identity;
    #endif
    
        private object baseObject;
        
        // a hash table hashed on property names, with a ExtenderItem of the property and the
        // object it belongs to.
        private Hashtable extenderList;
        private Dictionary<string, object> extenderDictionaryCorrected = new Dictionary<string, object>();
        private Dictionary<object, ExtendedObjectWrapper> collection = null;

        internal ExtendedObjectWrapper(object baseObject, Hashtable extenderList, Dictionary<object, ExtendedObjectWrapper> collection, object originalBaseObject) {
        #if DEBUG
            this.identity = ++count;
        #endif 
            this.baseObject = baseObject;
            this.collection = collection != null ? collection : new Dictionary<object, ExtendedObjectWrapper>(); 
            this.extenderList = CreateExtendedProperties(extenderList);
            this.collection[originalBaseObject] = this;
        }


        /// <devdoc>
        ///     Creates the extended descriptors for an object given a list of extenders
        ///     and property names.
        /// </devdoc>
        private Hashtable CreateExtendedProperties(Hashtable extenderList) {
            string[] keys = new string[extenderList.Keys.Count];
            extenderList.Keys.CopyTo(keys, 0);
            Hashtable extenders = new Hashtable();

            for (int i = 0; i < keys.Length; i++) {
                string name = keys[i];
                object extender = extenderList[name];
                this.extenderDictionaryCorrected[name] = extender;
                PropertyDescriptorCollection props = TypeDescriptor.GetProperties(extender, new Attribute[0]); // INTENTIONAL CHANGE from VSIP version of AutomationExtenderManager.cs: passing in empty Attribute array instead of {BrowsableAttribute.Yes} - see comments at beginning
                props.Sort();

                if (props != null) {
                    for (int p = 0; p < props.Count; p++) {
                        #if DEBUG
                            string pname = props[p].Name;
                            Debug.Assert(extenders[pname] == null, "multiple extenders of name '" + pname + "' detected");
                        #endif
                        extenders[props[p].Name] = new ExtenderItem(props[p], extender, this, name);
                    }
                }
            }
            return extenders;
        }

        /// <devdoc>
        ///     Retrieves an array of member attributes for the given object.
        /// </devdoc>
        AttributeCollection ICustomTypeDescriptor.GetAttributes() {
            return TypeDescriptor.GetAttributes(baseObject);
        }

        /// <devdoc>
        ///     Retrieves the class name for this object.  If null is returned,
        ///     the type name is used.
        /// </devdoc>
        string ICustomTypeDescriptor.GetClassName() {
            return TypeDescriptor.GetClassName(baseObject);
        }

        /// <devdoc>
        ///     Retrieves the name for this object.  If null is returned,
        ///     the default is used.
        /// </devdoc>
        string ICustomTypeDescriptor.GetComponentName() {
            return TypeDescriptor.GetComponentName(baseObject);
        }

        TypeConverter ICustomTypeDescriptor.GetConverter() {
            return TypeDescriptor.GetConverter(baseObject);
        }

        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() {
            return null;
        }


        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() {
            return TypeDescriptor.GetDefaultProperty(baseObject);
        }

        object ICustomTypeDescriptor.GetEditor(Type editorBaseType) {
            return TypeDescriptor.GetEditor(baseObject, editorBaseType);
        }

        /// <devdoc>
        ///     Retrieves an array of events that the given component instance
        ///     provides.  This may differ from the set of events the class
        ///     provides.  If the component is sited, the site may add or remove
        ///     additional events.
        /// </devdoc>
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents() {
            return TypeDescriptor.GetEvents(baseObject);
        }

        /// <devdoc>
        ///     Retrieves an array of events that the given component instance
        ///     provides.  This may differ from the set of events the class
        ///     provides.  If the component is sited, the site may add or remove
        ///     additional events.  The returned array of events will be
        ///     filtered by the given set of attributes.
        /// </devdoc>
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) {
            return TypeDescriptor.GetEvents(baseObject, attributes);
        }

        /// <devdoc>
        ///     Retrieves an array of properties that the given component instance
        ///     provides.  This may differ from the set of properties the class
        ///     provides.  If the component is sited, the site may add or remove
        ///     additional properties.
        /// </devdoc>
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() {
            return ((ICustomTypeDescriptor)this).GetProperties(new Attribute[0]);
        }

        /// <devdoc>
        ///     Retrieves an array of properties that the given component instance
        ///     provides.  This may differ from the set of properties the class
        ///     provides.  If the component is sited, the site may add or remove
        ///     additional properties.  The returned array of properties will be
        ///     filtered by the given set of attributes.
        /// </devdoc>
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes) {
            PropertyDescriptorCollection baseProps = TypeDescriptor.GetProperties(baseObject, attributes);

            PropertyDescriptor[] extProps = new PropertyDescriptor[extenderList.Count];

            IEnumerator propEnum = extenderList.Values.GetEnumerator();
            int count = 0;

            while (propEnum.MoveNext()) {
                PropertyDescriptor pd = (PropertyDescriptor)propEnum.Current;
                if (pd.Attributes.Contains(attributes)) {
                    extProps[count++] = pd;
                }
            }

            PropertyDescriptor[] allProps = new PropertyDescriptor[baseProps.Count + count];
            baseProps.CopyTo(allProps, 0);
            Array.Copy(extProps, 0, allProps, baseProps.Count, count);
            return new PropertyDescriptorCollection(allProps);
        }

        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) {
            if (pd != null) {
                ExtenderItem item = (ExtenderItem)extenderList[pd.Name];
                if (item != null && (pd == item.property || pd == item)) {
                    return item.extender;
                }
            }
            
            object unwrappedObject = baseObject;
            
            while (unwrappedObject is ICustomTypeDescriptor) {
                object lastObj = unwrappedObject;
                unwrappedObject = ((ICustomTypeDescriptor)unwrappedObject).GetPropertyOwner(pd);
                if (lastObj == unwrappedObject) {
                    break;
                }
            }
            
            return unwrappedObject;
        }

        internal object GetCorrectExtender(object extender, string propertyKey, object component)
        {
            ExtendedObjectWrapper correctWrapper = this;

            if (component != null)
            {
                if (component is ExtendedObjectWrapper)
                {
                    correctWrapper = component as ExtendedObjectWrapper;
                    if (correctWrapper == null)
                    {
                        correctWrapper = this;
                    }
                }
                else
                {
                    if (!collection.TryGetValue(component, out correctWrapper) || correctWrapper == null)
                    {
                        correctWrapper = this;
                    }
                }
            }
            

            object correctExtender = extender;
            if (correctWrapper != null && (!correctWrapper.extenderDictionaryCorrected.TryGetValue(propertyKey, out correctExtender) || correctExtender == null))
            {
                correctExtender = extender;
            }

            return correctExtender;
        }

        private class ExtenderItem : PropertyDescriptor {
            internal readonly PropertyDescriptor property;
            internal readonly object             extender;
            internal ExtendedObjectWrapper owner;
            internal string propertyKey;

            internal ExtenderItem(PropertyDescriptor prop, object extenderObject, ExtendedObjectWrapper owner, string propertyKey) : base(prop) {
                Debug.Assert(prop != null, "Null property passed to ExtenderItem");
                Debug.Assert(extenderObject != null, "Null extenderObject passed to ExtenderItem");
                this.property = prop;
                this.extender = extenderObject;
                this.owner = owner;
                this.propertyKey = propertyKey;
            }

            internal object CorrectExtender(object component)
            {
                return owner.GetCorrectExtender(extender, propertyKey, component);
            }
            
            public override Type ComponentType {
                get {
                    return property.ComponentType;
                }
            }

            public override TypeConverter Converter {
                get {
                    return property.Converter;
                }
            }
        
            public override bool IsLocalizable {
                get {
                    return property.IsLocalizable;
               }
            }
            
            public override bool IsReadOnly { 
                get{
                    return property.IsReadOnly;
                }
            }

            public override Type PropertyType { 
                get{
                    return property.PropertyType;
                }
            }
            
            public override bool CanResetValue(object component) {
                return property.CanResetValue(CorrectExtender(component));
            }
            
            public override string DisplayName {
                get {
                    return property.DisplayName;
                }
            }

            public override object GetEditor(Type editorBaseType) {
                return property.GetEditor(editorBaseType);
            }
            
            public override object GetValue(object component) {
                return property.GetValue(CorrectExtender(component));
            }
            
            public override void ResetValue(object component) {
                property.ResetValue(CorrectExtender(component));
            }

            public override void SetValue(object component, object value) {
                property.SetValue(CorrectExtender(component), value);
            }


            public override bool ShouldSerializeValue(object component) {
                return property.ShouldSerializeValue(CorrectExtender(component));
            }


            // BEGIN INTENTIONAL CHANGES from VSIP version of AutomationExtenderManager.cs
            //   Clients interested in receiving the ValueChanged
            //   event through the ExtenderItem property descriptor subclass
            //   need to have their request forwarded to the wrapped property,
            //   which is where the change will actually take place and from
            //   wheret the ValueChanged event will actually be fired.  So we override
            //   AddValueChanged and RemoveValueChanged.  Note that
            //   like SetValue, we pass in the extender as the component, ignoring
            //   the component passed in.  Otherwise the event would not fire properly.

            public override void AddValueChanged(object component, EventHandler handler)
            {
                property.AddValueChanged(CorrectExtender(component), handler);
            }

            public override void RemoveValueChanged(object component, EventHandler handler)
            {
                property.RemoveValueChanged(CorrectExtender(component), handler);
            }

            // END INTENTIONAL CHANGES


        }
    }
    
    internal class FilteredObjectWrapper : ICustomTypeDescriptor {
        private object baseObject;
        private Hashtable filteredProps;

        internal FilteredObjectWrapper(object baseObject) {
            this.baseObject = baseObject;
            this.filteredProps = new Hashtable();
        }
        
        /// <devdoc>
        ///     Filters the given property with the given member attribute.  We only
        ///     support filtering by adding a single attribute here.
        /// </devdoc>
        internal void FilterProperty(PropertyDescriptor prop, Attribute attr) {
            filteredProps[prop] = attr;
        }

        /// <devdoc>
        ///     Retrieves an array of member attributes for the given object.
        /// </devdoc>
        AttributeCollection ICustomTypeDescriptor.GetAttributes() {
            return TypeDescriptor.GetAttributes(baseObject);
        }

        /// <devdoc>
        ///     Retrieves the class name for this object.  If null is returned,
        ///     the type name is used.
        /// </devdoc>
        string ICustomTypeDescriptor.GetClassName() {
            return TypeDescriptor.GetClassName(baseObject);
        }

        /// <devdoc>
        ///     Retrieves the name for this object.  If null is returned,
        ///     the default is used.
        /// </devdoc>
        string ICustomTypeDescriptor.GetComponentName() {
            return TypeDescriptor.GetComponentName(baseObject);
        }

        TypeConverter ICustomTypeDescriptor.GetConverter() {
            return TypeDescriptor.GetConverter(baseObject);
        }

        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() {
            return null;
        }

        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() {
            return TypeDescriptor.GetDefaultProperty(baseObject);
        }

        object ICustomTypeDescriptor.GetEditor(Type editorBaseType) {
            return TypeDescriptor.GetEditor(baseObject, editorBaseType);
        }

        /// <devdoc>
        ///     Retrieves an array of events that the given component instance
        ///     provides.  This may differ from the set of events the class
        ///     provides.  If the component is sited, the site may add or remove
        ///     additional events.
        /// </devdoc>
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents() {
            return TypeDescriptor.GetEvents(baseObject);
        }

        /// <devdoc>
        ///     Retrieves an array of events that the given component instance
        ///     provides.  This may differ from the set of events the class
        ///     provides.  If the component is sited, the site may add or remove
        ///     additional events.  The returned array of events will be
        ///     filtered by the given set of attributes.
        /// </devdoc>
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) {
            return TypeDescriptor.GetEvents(baseObject, attributes);
        }

        /// <devdoc>
        ///     Retrieves an array of properties that the given component instance
        ///     provides.  This may differ from the set of properties the class
        ///     provides.  If the component is sited, the site may add or remove
        ///     additional properties.
        /// </devdoc>
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() {
            return ((ICustomTypeDescriptor)this).GetProperties(new Attribute[0]);
        }

        /// <devdoc>
        ///     Retrieves an array of properties that the given component instance
        ///     provides.  This may differ from the set of properties the class
        ///     provides.  If the component is sited, the site may add or remove
        ///     additional properties.  The returned array of properties will be
        ///     filtered by the given set of attributes.
        /// </devdoc>
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes) {
            PropertyDescriptorCollection baseProps = TypeDescriptor.GetProperties(baseObject, attributes);
            
            if (filteredProps.Keys.Count > 0) {
                ArrayList propList = new ArrayList();
                
                foreach (PropertyDescriptor prop in baseProps) {
                    Attribute attr = (Attribute)filteredProps[prop];
                    if (attr != null) {
                        //BEGIN INTENTIONAL CHANGES from VSIP version of AutomationExtenderManager.cs
                        PropertyDescriptor filteredProp;
                        if (attr is System.ComponentModel.ReadOnlyAttribute)
                        {
                            // If we're filtering this property to be read-only, we want to simply wrap it
                            //   with a class that makes the property appear read-only.  We don't use 
                            //   TypeDescriptor.CreateProperty() because it doesn't work well with
                            //   Com2PropertyDescriptor (gives an exception trying to get the value).
                            //   We can't change this behavior for properties we're "hiding"
                            //   without changing current behavior in the project designer.
                            filteredProp = new ReadOnlyPropertyDescriptorWrapper(prop);
                        }
                        else
                        {
                            filteredProp = TypeDescriptor.CreateProperty(baseObject.GetType(), prop, attr);
                        }
                        //END INTENTIONAL CHANGES

                        if (filteredProp.Attributes.Contains(attributes))
                        {
                            propList.Add(filteredProp);
                        }
                    }
                    else {
                        propList.Add(prop);
                    }
                }
                
                PropertyDescriptor[] propArray = new PropertyDescriptor[propList.Count];
                propList.CopyTo(propArray, 0);
                baseProps = new PropertyDescriptorCollection(propArray);
            }
            
            return baseProps;
        }

        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) {
            return baseObject;
        }


        ////////////////////////////////////////

        /// <summary>
        /// Wraps a PropertyDescriptor but changes it to read-only.  All other
        ///   functionality is delegated to the original property descriptor.
        /// </summary>
        internal class ReadOnlyPropertyDescriptorWrapper : PropertyDescriptor
        {
            PropertyDescriptor prop;

            public ReadOnlyPropertyDescriptorWrapper(PropertyDescriptor prop)
                : base(prop.Name, AttributeCollectionToArray(prop.Attributes))
            {
                this.prop = prop;
            }

            /// <summary>
            /// Creates an Attribute array from an AttributeCollection instance
            /// </summary>
            /// <param name="collection"></param>
            /// <returns></returns>
            private static Attribute[] AttributeCollectionToArray(AttributeCollection collection)
            {
                Attribute[] array = new Attribute[collection.Count];
                collection.CopyTo(array, 0);
                return array;
            }

            public override bool CanResetValue(object component)
            {
                return false; //override original property descriptor
            }

            public override Type ComponentType
            {
                get
                {
                    return prop.ComponentType;
                }
            }

            public override object GetValue(object component)
            {
                return prop.GetValue(component);
            }

            public override bool IsReadOnly
            {
                get
                {
                    return true; //override original property descriptor
                }
            }

            public override Type PropertyType
            {
                get
                {
                    return prop.PropertyType;
                }
            }

            public override void ResetValue(object component)
            {
                throw new NotSupportedException(); //override original property descriptor
            }

            public override void SetValue(object component, object value)
            {
                throw new NotSupportedException(); //override original property descriptor
            }

            public override bool ShouldSerializeValue(object component)
            {
                return prop.ShouldSerializeValue(component);
            }

            public override TypeConverter Converter
            {
                get
                {
                    return prop.Converter;
                }
            }
        }



    }
}

