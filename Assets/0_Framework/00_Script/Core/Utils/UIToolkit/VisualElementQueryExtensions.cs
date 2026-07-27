using System;
using System.Linq;
using O2un.Utils;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace O2un.Core.Utils
{
    public static class VisualElementQueryExtensions
    {
        public static T QRequired<T>(this VisualElement root, string name) where T : VisualElement
        {
            root.ThrowIfNull();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A UXML element name is required.", nameof(name));
            }

            T element = root.Q<T>(name: name);
            if (element != null)
            {
                return element;
            }
            
            throw new InvalidOperationException($"Required UI Toolkit element was not found. " + $"Root='{root.name}', Name='{name}', Type='{typeof(T).Name}'.");
        }

        public static T QRequiredByClass<T>(this VisualElement root, string className) where T : VisualElement
        {
            root.ThrowIfNull();
            if (string.IsNullOrWhiteSpace(className))
            {
                throw new ArgumentException("A USS class name is required.", nameof(className));
            }

            T element = root.Q<T>(className: className);
            if (element != null)
            {
                return element;
            }

            throw new InvalidOperationException($"Required UI Toolkit element was not found. " + $"Root='{root.name}', Class='{className}', Type='{typeof(T).Name}'.");
        }

        public static T QOptional<T>(this VisualElement root, string name) where T : VisualElement
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return root.Q<T>(name: name);
        }

        public static T QOptionalByClass<T>(this VisualElement root, string name) where T : VisualElement
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return root.Q<T>(className: name);
        }

        public static bool TryQ<T>(this VisualElement root, string name, out T element) where T : VisualElement
        {
            element = root.QOptional<T>(name);
            return element != null;
        }

        public static VisualElementBinding<T> QRequiredBinding<T>( this VisualElement root, string name) where T : VisualElement
        {
            return new VisualElementBinding<T>(root.QRequired<T>(name));
        }

        public static VisualElementBinding<T> QRequiredBindingByClass<T>(this VisualElement root, string className) where T : VisualElement
        {
            return new VisualElementBinding<T>(root.QRequiredByClass<T>(className));
        }

        public static VisualElementBinding<T> QOptionalBinding<T>( this VisualElement root, string name) where T : VisualElement
        {
            var element = root.QOptional<T>(name);
            if(null == element)
            {
                return default; 
            }
            return new VisualElementBinding<T>(element);
        }

        public static VisualElementBinding<T> QOptionalBindingByClass<T>(this VisualElement root, string className) where T : VisualElement
        {
            var element = root.QOptionalByClass<T>(className);
            if(null == element)
            {
                return default; 
            }
            return new VisualElementBinding<T>(element);
        }
    }
}
