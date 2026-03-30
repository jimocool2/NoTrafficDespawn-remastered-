using Game;
using NoTrafficDespawn;

namespace Systems
{
    public partial class NoTrafficDespawnSystem : GameSystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var setting = Mod.CurrentSetting;
            if (setting?.Enabled != true)
            {
                return;
            }

            // Traffic despawn port logic goes here.
        }
    }
}
