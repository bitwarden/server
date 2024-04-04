using System.Collections;
using Microsoft.Azure.Cosmos.Table;

namespace Bit.Core.Models.Data;

public class DictionaryEntity : TableEntity, IDictionary<string, EntityProperty>
{
    private IDictionary<string, EntityProperty> _properties = new Dictionary<string, EntityProperty>();

    public ICollection<EntityProperty> Values => _properties.Values;

    public EntityProperty this[string key]
    {
        get => _properties[key];
        set => _properties[key] = value;
    }

    public int Count => _properties.Count;

    public bool IsReadOnly => _properties.IsReadOnly;

    public ICollection<string> Keys => _properties.Keys;

    public override void ReadEntity(IDictionary<string, EntityProperty> properties,
        OperationContext operationContext)
    {
        _properties = properties;
    }

    public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
    {
        return _properties;
    }

    public void Add(string key, EntityProperty value)
    {
        _properties.Add(key, value);
    }

    public void Add(string key, bool value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(string key, byte[] value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(string key, DateTime? value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(string key, DateTimeOffset? value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(string key, double value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(string key, Guid value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(string key, int value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(string key, long value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(string key, string value)
    {
        _properties.Add(key, new EntityProperty(value));
    }

    public void Add(KeyValuePair<string, EntityProperty> item)
    {
        _properties.Add(item);
    }

    public bool ContainsKey(string key)
    {
        return _properties.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        return _properties.Remove(key);
    }

    public bool TryGetValue(string key, out EntityProperty value)
    {
        return _properties.TryGetValue(key, out value);
    }

    public void Clear()
    {
        _properties.Clear();
    }

    public bool Contains(KeyValuePair<string, EntityProperty> item)
    {
        return _properties.Contains(item);
    }

    public void CopyTo(KeyValuePair<string, EntityProperty>[] array, int arrayIndex)
    {
        _properties.CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, EntityProperty> item)
    {
        return _properties.Remove(item);
    }

    public IEnumerator<KeyValuePair<string, EntityProperty>> GetEnumerator()
    {
        return _properties.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _properties.GetEnumerator();
    }
}
