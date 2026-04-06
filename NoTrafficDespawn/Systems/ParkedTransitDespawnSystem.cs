using System;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using NoTrafficDespawn.Helpers;
using Unity.Collections;
using Unity.Entities;

namespace NoTrafficDespawn.Systems
{
    public partial class ParkedTransitDespawnSystem : GameSystemBase
    {
        private EntityQuery m_ParkedTransitQuery;
        private SimulationSystem m_SimulationSystem;
        private DisableTrafficDespawnSystem m_DisableTrafficDespawnSystem;
        private EntityCommandBufferSystem m_EntityCommandBufferSystem;
        private PrefixLogger m_Log;

        private bool m_ShouldDespawnTransit => this.m_DisableTrafficDespawnSystem.despawnBehavior != DespawnBehavior.NoDespawn || this.m_DisableTrafficDespawnSystem.DespawnPublicTransit || this.m_DisableTrafficDespawnSystem.DespawnAll;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 8;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_Log = new PrefixLogger(nameof(ParkedTransitDespawnSystem));
            m_Log.Debug(nameof(OnCreate));

            this.m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_DisableTrafficDespawnSystem = World.GetOrCreateSystemManaged<DisableTrafficDespawnSystem>();
            this.m_EntityCommandBufferSystem = World.GetOrCreateSystemManaged<ModificationBarrier1>();

            this.m_ParkedTransitQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PassengerTransport>()
                },
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<ParkedCar>(),
                    ComponentType.ReadOnly<ParkedTrain>(),
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Building>(),
                }
            });

            RequireForUpdate(this.m_ParkedTransitQuery);
        }

        protected override void OnUpdate()
        {
            try
            {
                if (this.m_SimulationSystem.selectedSpeed <= 0 || !this.m_ShouldDespawnTransit)
                {
                    return;
                }

                NativeArray<Entity> parkedTransitEntities = this.m_ParkedTransitQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    EntityCommandBuffer commandBuffer = this.m_EntityCommandBufferSystem.CreateCommandBuffer();

                    for (int i = 0; i < parkedTransitEntities.Length; i++)
                    {
                        Entity entity = parkedTransitEntities[i];
                        commandBuffer.AddComponent<Deleted>(entity);
                        commandBuffer.AddComponent<BatchesUpdated>(entity);
                    }
                }
                finally
                {
                    parkedTransitEntities.Dispose();
                }
            }
            catch (Exception ex)
            {
                m_Log.Error($"Failed in OnUpdate: {ex}");
                throw;
            }
        }
    }
}
