// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;
using YamlDotNet.Serialization.TypeInspectors;

// ref: https://github.com/aaubry/YamlDotNet/issues/152#issuecomment-349034754

namespace Bit.Setup;

public class CommentGatheringTypeInspector : TypeInspectorSkeleton
{
    private readonly ITypeInspector _innerTypeDescriptor;

    public CommentGatheringTypeInspector(ITypeInspector innerTypeDescriptor)
    {
        _innerTypeDescriptor = innerTypeDescriptor ?? throw new ArgumentNullException(nameof(innerTypeDescriptor));
    }

    public override string GetEnumName(Type enumType, string name)
    {
        return _innerTypeDescriptor.GetEnumName(enumType, name);
    }

    public override string GetEnumValue(object enumValue)
    {
        return _innerTypeDescriptor.GetEnumValue(enumValue);
    }

    public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container)
    {
        return _innerTypeDescriptor.GetProperties(type, container).Select(d => new CommentsPropertyDescriptor(d));
    }

    private sealed class CommentsPropertyDescriptor : IPropertyDescriptor
    {
        private readonly IPropertyDescriptor _baseDescriptor;

        public CommentsPropertyDescriptor(IPropertyDescriptor baseDescriptor)
        {
            _baseDescriptor = baseDescriptor;
        }

        public string Name => _baseDescriptor.Name;
        public int Order
        {
            get => _baseDescriptor.Order;
            set => _baseDescriptor.Order = value;
        }
        public Type Type => _baseDescriptor.Type;
        public bool CanWrite => _baseDescriptor.CanWrite;
        public bool AllowNulls => _baseDescriptor.AllowNulls;
        public bool Required => _baseDescriptor.Required;
        public Type ConverterType => _baseDescriptor.ConverterType;

        public Type TypeOverride
        {
            get { return _baseDescriptor.TypeOverride; }
            set { _baseDescriptor.TypeOverride = value; }
        }

        public ScalarStyle ScalarStyle
        {
            get { return _baseDescriptor.ScalarStyle; }
            set { _baseDescriptor.ScalarStyle = value; }
        }

        public void Write(object target, object value)
        {
            _baseDescriptor.Write(target, value);
        }

        public T GetCustomAttribute<T>() where T : Attribute
        {
            return _baseDescriptor.GetCustomAttribute<T>();
        }

        public IObjectDescriptor Read(object target)
        {
            var description = _baseDescriptor.GetCustomAttribute<DescriptionAttribute>();
            return description != null ?
                new CommentsObjectDescriptor(_baseDescriptor.Read(target), description.Description) :
                _baseDescriptor.Read(target);
        }
    }
}

public sealed class CommentsObjectDescriptor : IObjectDescriptor
{
    private readonly IObjectDescriptor _innerDescriptor;

    public CommentsObjectDescriptor(IObjectDescriptor innerDescriptor, string comment)
    {
        _innerDescriptor = innerDescriptor;
        Comment = comment;
    }

    public string Comment { get; private set; }
    public object Value => _innerDescriptor.Value;
    public Type Type => _innerDescriptor.Type;
    public Type StaticType => _innerDescriptor.StaticType;
    public ScalarStyle ScalarStyle => _innerDescriptor.ScalarStyle;
}

public class CommentsObjectGraphVisitor : ChainedObjectGraphVisitor
{
    public CommentsObjectGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor)
        : base(nextVisitor) { }

    public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context, ObjectSerializer serializer)
    {
        if (value is CommentsObjectDescriptor commentsDescriptor && commentsDescriptor.Comment != null)
        {
            context.Emit(new Comment(string.Empty, false));
            foreach (var comment in commentsDescriptor.Comment.Split(Environment.NewLine))
            {
                context.Emit(new Comment(comment, false));
            }
        }
        return base.EnterMapping(key, value, context, serializer);
    }
}
