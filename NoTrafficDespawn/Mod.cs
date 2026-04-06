using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using NoTrafficDespawn.Helpers;
using NoTrafficDespawn.Systems;
using System.Reflection;

namespace NoTrafficDespawn
{
    public class Mod : IMod
    {
        public const string ModName = "NoTrafficDespawn (Remastered)";

        public static Mod Instance { get; private set; }

        public ILog Log { get; private set; }
        public TrafficDespawnSettings settings { get; private set; }

        private PrefixLogger m_Log;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;

            // Initialize logger.
            Log = LogManager
                  .GetLogger(ModName)
                  .SetShowsErrorsInUI(false);
#if IS_DEBUG
            Log = Log
                  .SetBacktraceEnabled(true)
                  .SetEffectiveness(Level.All);
#endif
            m_Log = new PrefixLogger(nameof(Mod));
            m_Log.Info($"Loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");


            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Log.Info($"Current mod asset at {asset.path}");

            settings = new TrafficDespawnSettings(this);
            settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(settings));

            // Load saved settings
            AssetDatabase.global.LoadSettings(nameof(NoTrafficDespawn), settings, new TrafficDespawnSettings(this));

            updateSystem.UpdateBefore<NewStuckMovingObjectSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAfter<DisableTrafficDespawnSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAfter<ParkedTransitDespawnSystem>(SystemUpdatePhase.Modification1);
        }

        public void OnDispose()
        {
            m_Log.Info("Disposing");
            Instance = null;

            if (settings != null)
            {
                settings.UnregisterInOptionsUI();
                settings = null;
            }
        }
    }
}
