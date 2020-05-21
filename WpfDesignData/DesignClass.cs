using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml.Serialization;

namespace WpfDesignData
{
    [DefaultProperty("Items")]
    [ContentProperty("Items")]
    public class DesignCollection : MarkupExtension
    {
        public Collection<object> Items { get; set; } = new Collection<object>();

        private object ProvideItemValue(object obj, IServiceProvider serviceProvider)
        {
            switch (obj)
            {
                case DesignClass designClass:
                    return designClass.ProvideValue(serviceProvider);
                default:
                    return obj;
            }
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Items.Select(x => ProvideItemValue(x, serviceProvider));
        }
    }

    [DefaultProperty("Properties")]
    [ContentProperty("Properties")]
    public class DesignClass : MarkupExtension
    {
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        public Type TargetType { get; set; }


        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var obj = new ExpandoObject();
            var dict = (IDictionary<string, object>)obj;

            foreach (var designProperty in Properties)
            {
                dict[designProperty.Key] = designProperty.Value;
            }

            if (TargetType != null)
            {
                dict["MasqueradeAsType"] = TargetType;
            }

            return obj;
        }

    }

    [DefaultProperty("Properties")]
    [ContentProperty("Properties")]
    public class DesignClass2 : MarkupExtension
    {
        public Collection<DesignProperty> Properties { get; set; } = new Collection<DesignProperty>();

        public Type TargetType { get; set; }


        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            //if (TargetType != null)
            //{
            //    var s1 = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
            //    var target = s1.TargetObject;

            //    if (target is FrameworkElement frameworkElement)
            //    {
            //        var template = frameworkElement.TryFindResource(new DataTemplateKey(TargetType)) as DataTemplate;

            //        frameworkElement.Resources.InvalidatesImplicitDataTemplateResources = true;
            //    }
            //}

            var obj = new ExpandoObject();
            var dict = (IDictionary<string, object>)obj;

            foreach (var designProperty in Properties)
            {
                dict[designProperty.Key] = designProperty.Value;
            }

            if (TargetType != null)
            {
                dict["MasqueradeAsType"] = TargetType;
            }

            return obj;
        }

    }

    [DefaultProperty("Value")]
    [ContentProperty("Value")]
    public class DesignProperty
    {
        public string Key { get; set; }
        public object Value { get; set; }
    }

    public class DesignDataTypeMapper : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var contentControl = new FrameworkElementFactory(typeof(ContentControl));
            contentControl.SetBinding(ContentControl.ContentProperty, new Binding() { Path = new PropertyPath(".") });
            contentControl.SetValue(ContentControl.ContentTemplateSelectorProperty, new DesignTemplateSelector());

            return new DataTemplate(typeof(ExpandoObject))
            {
                VisualTree = contentControl
            };
        }
    }

    [DictionaryKeyProperty("DataTemplateKey")]
    public class DesignDataTemplate : DataTemplate
    {
        public DesignDataTemplate()
            : base(typeof(ExpandoObject))
        {
            var contentControl = new FrameworkElementFactory(typeof(ContentControl));
            contentControl.SetBinding(ContentControl.ContentProperty, new Binding() { Path = new PropertyPath(".") });
            contentControl.SetValue(ContentControl.ContentTemplateSelectorProperty, new DesignTemplateSelector());

            VisualTree = contentControl;
        }

        protected override void ValidateTemplatedParent(FrameworkElement templatedParent)
        {
            base.ValidateTemplatedParent(templatedParent);
        }
    }


    public class DesignTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            FrameworkElement element = container as FrameworkElement;

            if (item is ExpandoObject obj)
            {
                var dict = (IDictionary<string, object>)obj;
                if (dict.TryGetValue("MasqueradeAsType", out var type) && type != null)
                {
                    return element.TryFindResource(new DataTemplateKey(type)) as DataTemplate;
                }
                else
                {
                    var sb = new StringBuilder();

                    foreach (var kvp in dict)
                    {
                        sb.AppendLine($"{kvp.Key}: {kvp.Value}");
                    }

                    var contentControl = new FrameworkElementFactory(typeof(TextBlock));
                    contentControl.SetValue(TextBlock.TextProperty, sb.ToString());

                    return new DataTemplate()
                    {
                        VisualTree = contentControl
                    };
                }
            }
            else
            {
                var contentControl = new FrameworkElementFactory(typeof(TextBlock));
                contentControl.SetValue(TextBlock.TextProperty, "<unknown>");

                return new DataTemplate()
                {
                    VisualTree = contentControl
                };
            }
        }
    }

    public class DynamicDictionary : DynamicObject
    {
        private readonly Type _targetType;
        Dictionary<string, object> _properties;

        public DynamicDictionary(IEnumerable<DesignProperty> properties, Type targetType)
        {
            _properties = properties.ToDictionary(x => x.Key, x => x.Value);
            _targetType = targetType;
        }

        public int Count
        {
            get
            {
                return _properties.Count;
            }
        }

        public override DynamicMetaObject GetMetaObject(System.Linq.Expressions.Expression parameter)
        {
            var t = base.GetMetaObject(parameter);

            return t;
        }

        public override bool TryGetMember(
            GetMemberBinder binder, out object result)
        {
            string name = binder.Name;

            if (_targetType != null && binder.Name == "GetType")
            {
                result = new DynamicMethod(binder.Name, typeof(Type), new Type[] { });
                return true;
            }

            return _properties.TryGetValue(name, out result);
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (_targetType != null && binder.Name == "GetType")
            {
                result = _targetType;
                return true;
            }

            return base.TryInvokeMember(binder, args, out result);
        }

        public override bool TrySetMember(
            SetMemberBinder binder, object value)
        {
            _properties[binder.Name] = value;

            return true;
        }
    }
}
