using Autofac;

namespace NanoBus
{
    public class InProcessMediator : InProcessScopedMediator
    {
        public InProcessMediator(ILifetimeScope lifetimeScope) : base(lifetimeScope.BeginLifetimeScope, true)
        {
        }
    }
}