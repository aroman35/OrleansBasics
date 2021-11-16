using System;

namespace Grains
{
    public class MongoCollectionAttribute : Attribute
    {
        public MongoCollectionAttribute(string collectionName)
        {
            CollectionName = collectionName;
        }

        public string CollectionName { get; }
    }
}