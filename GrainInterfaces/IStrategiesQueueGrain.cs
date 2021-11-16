using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace GrainInterfaces
{
    public interface IStrategiesQueueGrain : IGrainWithGuidKey
    {
        Task SendDataToStream(int data);
        Task SendDataToStream(IEnumerable<int> data);
    }
}