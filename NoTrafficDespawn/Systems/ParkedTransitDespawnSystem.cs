using System;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace NoTrafficDespawn
{
    public partial class ParkedTransitDespawnSystem : GameSystemBase
    {
        private EntityQuery parkedTransitQuery;
        private SimulationSystem simulationSystem;
        private DisableTrafficDespawnSystem disableTrafficDespawnSystem;
        private EntityCommandBufferSystem entityCommandBufferSystem;

        private bool ShoulDespawnTransit => this.disableTrafficDespawnSystem.despawnBehavior != DespawnBehavior.NoDespawn || this.disableTrafficDespawnSystem.DespawnPublicTransit || this.disableTrafficDespawnSystem.DespawnAll;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 8;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            this.simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            this.disableTrafficDespawnSystem = World.GetOrCreateSystemManaged<DisableTrafficDespawnSystem>();
            this.entityCommandBufferSystem = World.GetOrCreateSystemManaged<ModificationBarrier1>();

            this.parkedTransitQuery = GetEntityQuery(new EntityQueryDesc
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

            RequireForUpdate(this.parkedTransitQuery);
        }

        protected override void OnUpdate()
        {
            try
            {
                if (this.simulationSystem.selectedSpeed <= 0 || !this.ShoulDespawnTransit)
                {
                    return;
                }

                NativeArray<Entity> parkedTransitEntities = this.parkedTransitQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    EntityCommandBuffer commandBuffer = this.entityCommandBufferSystem.CreateCommandBuffer();
                    //Mod.log.Info($"{nameof(ParkedTransitDespawnSystem)}: selectedSpeed={this.simulationSystem.selectedSpeed}, " +
                    //             $"despawnBehavior={this.disableTrafficDespawnSystem.despawnBehavior}, " +
                    //             $"parkedTransitCount={parkedTransitEntities.Length}");

                    for (int i = 0; i < parkedTransitEntities.Length; i++)
                    {
                        Entity entity = parkedTransitEntities[i];
                        //Mod.log.Info($"{nameof(ParkedTransitDespawnSystem)}: marking Deleted for parked transit entity {entity.Index}:{entity.Version}");
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
                Mod.log.Error($"{nameof(ParkedTransitDespawnSystem)} failed in OnUpdate: {ex}");
                throw;
            }
        }
    }
}
