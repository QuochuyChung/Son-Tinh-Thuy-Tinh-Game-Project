namespace SonTinhThuyTinh.Core
{
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }

    public sealed class StateMachine
    {
        public IState Current { get; private set; }

        public void ChangeState(IState next)
        {
            Current?.Exit();
            Current = next;
            Current.Enter();
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
    }
}
