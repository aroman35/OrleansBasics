using System;
using System.Threading.Tasks;
using Orleans;

namespace GrainInterfaces
{
    public interface IStrategy : IGrainWithGuidKey
    {
        Task<Guid> Create(string name, string binary);
        Task Load();
        Task Start(Guid agentId);
        Task UpdateProgress(double progress);
        Task Finish(string result);
    }
}