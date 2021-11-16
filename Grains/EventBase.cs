using MongoDB.Driver;

namespace Grains
{
    public abstract record EventBase<TState>
        where TState : class, new()
    {
        public abstract UpdateDefinition<TState> UpdateDefinition { get; }
    }
}