using ArangoDB.Client.Utility;
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace ArangoDB.Client.Property
{
    public class DatabaseCollectionSetting
    {
        ConcurrentDictionary<Type, CollectionPropertySetting> collectionProperties = new ConcurrentDictionary<Type, CollectionPropertySetting>();
        ConcurrentDictionary<IdentifierType, string> defaultIdentifierNames = new ConcurrentDictionary<IdentifierType, string>();

        ConcurrentDictionary<IdentifierType, Func<Type, string>> defaultIdentifierFuncs = new ConcurrentDictionary<IdentifierType, Func<Type, string>>();
        //ConcurrentDictionary<Type, Dictionary<IdentifierType, Func<Type, string>>> defaultIdentifierFuncs = new ConcurrentDictionary<Type, Dictionary<IdentifierType, Func<Type, string>>>();
        ConcurrentDictionary<Type, ConcurrentDictionary<IdentifierType, string>> defaultIdentifierNamesForType = new ConcurrentDictionary<Type, ConcurrentDictionary<IdentifierType, string>>();

        DatabaseSharedSetting setting;

        public DatabaseCollectionSetting(DatabaseSharedSetting setting)
        {
            this.setting = setting;
        }

        public void ChangeIdentifierDefaultName(IdentifierType identifier, Func<Type, string> func)
        {
            if (IdentifierType.None == identifier)
                throw new InvalidOperationException("Can not set default name for [IdentifierType.None]");

            defaultIdentifierFuncs.AddOrUpdate(identifier, func, (oldIdentifier, oldFunc) => func);

            setting.IdentifierModifier.ClearMethodCache();
        }

        internal bool FindIdentifierDefaultNameForType(Type type, IdentifierType identifier, string memberName)
        {
            ConcurrentDictionary<IdentifierType, string> identifierNames = null;
            if (defaultIdentifierNamesForType.TryGetValue(type, out identifierNames))
            {
                string resolvedName = null;
                if (identifierNames.TryGetValue(identifier, out resolvedName))
                    return memberName == resolvedName;
            }
            else
            {
                identifierNames = new ConcurrentDictionary<IdentifierType, string>();
                defaultIdentifierNamesForType.TryAdd(type, identifierNames);
            }

            Func<Type, string> f = null;
            if (!defaultIdentifierFuncs.TryGetValue(identifier, out f))
                return false;

            if (memberName == f(type))
            {
                identifierNames[identifier] = memberName;
                return true;
            }

            return false;
        }

        public void ChangeIdentifierDefaultName(IdentifierType identifier, string defaultName)
        {
            if (IdentifierType.None == identifier)
                throw new InvalidOperationException("Can not set default name for [IdentifierType.None]");

            defaultIdentifierNames.AddOrUpdate(identifier, defaultName, (oldIdentifier, oldName) => defaultName);

            setting.IdentifierModifier.ClearMethodCache();
        }

        internal void ChangeCollectionPropertyForType(Type type, Action<ICollectionPropertySetting> action)
        {
            action(collectionProperties.GetOrAdd(type, new CollectionPropertySetting()));

            setting.IdentifierModifier.ClearMethodCache(type);
        }

        public void ChangeCollectionPropertyForType<T>(Action<ICollectionPropertySetting> action)
        {
            ChangeCollectionPropertyForType(typeof(T), action);
        }

        internal void ChangeDocumentPropertyForType(Type type, string memberName, Action<IDocumentPropertySetting> action)
        {
            ChangeCollectionPropertyForType(type, collection =>
            {
                var collectionSetting = collection as CollectionPropertySetting;
                action(collectionSetting.FindDocumentPropertyForType(memberName));
            });

            setting.IdentifierModifier.ClearMethodCache(type);
        }

        public void ChangeDocumentPropertyForType<T>(Expression<Func<T, object>> attribute, Action<IDocumentPropertySetting> action)
        {
            ChangeCollectionPropertyForType<T>(collection =>
            {
                var collectionSetting = collection as CollectionPropertySetting;
                action(collectionSetting.FindDocumentPropertyForType<T>(attribute));
            });
        }

        public string ResolveCollectionName(Type type)
        {
            ICollectionPropertySetting collectionProperty = null;
            ChangeCollectionPropertyForType(type, x => collectionProperty = x);

            if (collectionProperty.CollectionName != null)
                return collectionProperty.CollectionName;

            ICollectionPropertySetting collectionAttribute = CollectionPropertySetting.FindCollectionAttributeForType(type);

            if (collectionAttribute != null && collectionAttribute.CollectionName != null)
                return collectionAttribute.CollectionName;

            return type.Name;
        }

        public string ResolveCollectionName<T>()
        {
            return ResolveCollectionName(typeof(T));
        }

        internal string ResolvePropertyName(Type type, string memberName)
        {
            IDocumentPropertySetting documentProperty = null;
            ChangeDocumentPropertyForType(type, memberName, x => documentProperty = x);

            /*************   defined setting   **************/

            // * return defined setting identifier
            var docAttributeName = documentProperty.Identifier.ToAttributeName();
            if (docAttributeName != null)
                return docAttributeName;

            // * return defined setting name 
            if (documentProperty.PropertyName != null)
                return documentProperty.PropertyName;

            // * return defined setting naming convention
            if (documentProperty.Naming == NamingConvention.ToCamelCase)
                return StringUtils.ToCamelCase(memberName);

            /*************   attribute setting   **************/

            var attributeProperty = DocumentPropertySetting.FindDocumentAttributeForType(type, memberName);

            if (attributeProperty != null)
            {
                // * return attribute setting identifier
                var attributeName = attributeProperty.Identifier.ToAttributeName();
                if (attributeName != null)
                    return attributeName;

                // * return defined setting name 
                if (attributeProperty.PropertyName != null)
                    return attributeProperty.PropertyName;

                // * return defined setting naming convention
                if (attributeProperty.Naming == NamingConvention.ToCamelCase)
                    return StringUtils.ToCamelCase(memberName);

            }

            /*************   default identifer setting for type   **************/
            var allIdentifierTypes = ArangoAttributes.GetAllIdentifierTypes();

            string defaultName = null;
            foreach (IdentifierType idType in allIdentifierTypes)
                if (FindIdentifierDefaultNameForType(type, idType, memberName) 
                    || (defaultIdentifierNames.TryGetValue(idType, out defaultName) && defaultName == memberName))
                    return idType.ToAttributeName();
 
            /*************   collection setting   **************/

            ICollectionPropertySetting collectionProperty = null;
            ChangeCollectionPropertyForType(type, x => collectionProperty = x);

            // * return defined setting collection naming convention
            if (collectionProperty.Naming == NamingConvention.ToCamelCase)
                return StringUtils.ToCamelCase(memberName);


            /*************  attribute collection setting   **************/

            ICollectionPropertySetting collectionAttribute = CollectionPropertySetting.FindCollectionAttributeForType(type);

            if (collectionAttribute != null)
            {
                if (collectionAttribute.Naming == NamingConvention.ToCamelCase)
                    return StringUtils.ToCamelCase(memberName);
            }

            return memberName;
        }

        public string ResolvePropertyName<T>(Expression<Func<T, object>> attribute)
        {
            var memberInfo = Utils.GetMemberInfo<T>(attribute);

            return ResolvePropertyName(typeof(T), memberInfo.Name);
        }

        internal string ResolveNestedPropertyName<T>(Expression<Func<T, object>> attribute)
        {
            var memberExpression = Utils.GetMemberExpression<T>(attribute);

            return ResolvePropertyName(memberExpression.Expression.Type, memberExpression.Member.Name);
        }
    }
}
