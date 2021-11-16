using Orleans;

namespace GrainInterfaces
{
    public interface IBacktestObserver : IGrainObserver
    {
        void Stop();
    }
}