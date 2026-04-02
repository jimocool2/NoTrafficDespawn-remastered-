using Game;
using Game.Buildings;
using Game.Common;
using Game.Creatures;
using Game.Pathfind;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace NoTrafficDespawn
{
    public partial class DisableTrafficDespawnSystem : GameSystemBase
    {
        private StuckMovingObjectSystem stuckMovingObjectSystem;
        private SimulationSystem simulationSystem;
        private EntityCommandBufferSystem entityCommandBufferSystem;
        private EntityQuery stuckObjectQuery;
        private EntityQuery unstuckObjectQuery;

        private bool highlightDirty;
        private bool wasHighlighting;
        private bool despawnAll;
        private bool despawnCommercialVehicles;
        private bool despawnPedestrians;
        private bool despawnPersonalVehicles;
        private bool despawnPublicTransit;
        private bool despawnServiceVehicles;
        private bool despawnTaxis;

        public DespawnBehavior despawnBehavior;
        public bool highlightStuckObjects;
        public int deadlockLingerFrames;
        public int deadlockSearchDepth;
        public int maxStuckObjectRemovalCount;
        public int maxStuckObjectSpeed;
        public bool DespawnAll => this.despawnAll;
        public bool DespawnPublicTransit => this.despawnPublicTransit;

        private bool shouldDisable;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 4;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            this.stuckMovingObjectSystem = World.GetOrCreateSystemManaged<StuckMovingObjectSystem>();
            this.simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            this.entityCommandBufferSystem = World.GetOrCreateSystemManaged<ModificationBarrier1>();

            Mod.INSTANCE.settings.onSettingsApplied += settings =>
            {
                if (settings is TrafficDespawnSettings despawnSettings)
                {
                    this.updateSettings(despawnSettings);
                }
            };

            this.updateSettings(Mod.INSTANCE.settings);

            this.unstuckObjectQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<UnstuckObject>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Building>(),
                }
            });

            this.stuckObjectQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<StuckObject>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Building>(),
                    ComponentType.ReadOnly<UnstuckObject>(),
                }
            });

            RequireForUpdate(this.stuckObjectQuery);
        }

        protected override void OnUpdate()
        {
            if (this.simulationSystem.selectedSpeed <= 0)
                return;

            if (this.shouldDisable)
            {
                this.cleanupAfterDisable();
                this.Enabled = false;
                return;
            }

            // --- Main thread: highlightDirty cleanup (infrequent) ---
            if (highlightDirty)
            {
                NativeArray<Entity> allStuck = this.stuckObjectQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < allStuck.Length; i++)
                {
                    if (EntityManager.HasComponent<Highlighted>(allStuck[i]))
                    {
                        EntityManager.RemoveComponent<Highlighted>(allStuck[i]);
                        EntityManager.AddComponent<BatchesUpdated>(allStuck[i]);
                    }
                    if (EntityManager.HasComponent<UnstuckObject>(allStuck[i]))
                    {
                        EntityManager.RemoveComponent<UnstuckObject>(allStuck[i]);
                        EntityManager.AddComponent<Updated>(allStuck[i]);
                    }
                }
                allStuck.Dispose();
                highlightDirty = false;
            }

            // --- Job: process stuck entities ---
            NativeReference<int> removalCount = new NativeReference<int>(
                this.maxStuckObjectRemovalCount, Allocator.TempJob);

            EntityCommandBuffer commandBuffer = this.entityCommandBufferSystem.CreateCommandBuffer();

            ProcessStuckEntitiesJob job = new ProcessStuckEntitiesJob
            {
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_StuckObjectType = SystemAPI.GetComponentTypeHandle<StuckObject>(isReadOnly: false),
                m_BlockerData = SystemAPI.GetComponentLookup<Blocker>(isReadOnly: true),
                m_DeliveryTruckData = SystemAPI.GetComponentLookup<DeliveryTruck>(isReadOnly: true),
                m_CreatureData = SystemAPI.GetComponentLookup<Creature>(isReadOnly: true),
                m_PersonalCarData = SystemAPI.GetComponentLookup<PersonalCar>(isReadOnly: true),
                m_PassengerTransportData = SystemAPI.GetComponentLookup<PassengerTransport>(isReadOnly: true),
                m_TaxiData = SystemAPI.GetComponentLookup<Taxi>(isReadOnly: true),
                m_PathOwnerData = SystemAPI.GetComponentLookup<PathOwner>(isReadOnly: false),
                commandBuffer = commandBuffer,
                availableRemovalCount = removalCount,
                despawnBehavior = this.despawnBehavior,
                highlightStuckObjects = this.highlightStuckObjects,
                despawnAll = this.despawnAll,
                despawnCommercialVehicles = this.despawnCommercialVehicles,
                despawnPedestrians = this.despawnPedestrians,
                despawnPersonalVehicles = this.despawnPersonalVehicles,
                despawnPublicTransit = this.despawnPublicTransit,
                despawnServiceVehicles = this.despawnServiceVehicles,
                despawnTaxis = this.despawnTaxis,
                deadlockLingerFrames = this.deadlockLingerFrames,
            };

            base.Dependency = JobChunkExtensions.Schedule(job, this.stuckObjectQuery, base.Dependency);
            this.entityCommandBufferSystem.AddJobHandleForProducer(base.Dependency);

            // NativeReference must be disposed after the job completes
            base.Dependency = removalCount.Dispose(base.Dependency);

            // --- Main thread: unstuck entity cleanup (infrequent) ---
            if (this.highlightStuckObjects)
            {
                NativeArray<Entity> unstuckEntities = this.unstuckObjectQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < unstuckEntities.Length; i++)
                {
                    EntityManager.RemoveComponent<StuckObject>(unstuckEntities[i]);
                    EntityManager.RemoveComponent<Highlighted>(unstuckEntities[i]);
                    EntityManager.RemoveComponent<UnstuckObject>(unstuckEntities[i]);
                    EntityManager.AddComponent<BatchesUpdated>(unstuckEntities[i]);
                    EntityManager.AddComponent<Updated>(unstuckEntities[i]);
                }
                unstuckEntities.Dispose();
            }
        }

        private void updateSettings(TrafficDespawnSettings settings)
        {
            this.stuckMovingObjectSystem.Enabled = settings.despawnBehavior == DespawnBehavior.Vanilla;
            this.shouldDisable = settings.despawnBehavior == DespawnBehavior.Vanilla;
            if (!this.shouldDisable)
            {
                this.Enabled = true;
            }

            this.despawnBehavior = settings.despawnBehavior;
            this.highlightStuckObjects = settings.highlightStuckObjects;
            this.deadlockLingerFrames = settings.deadlockLingerFrames;
            this.deadlockSearchDepth = settings.deadlockSearchDepth;
            this.maxStuckObjectRemovalCount = settings.maxStuckObjectRemovalCount;
            this.maxStuckObjectSpeed = settings.maxStuckObjectSpeed;
            if (this.wasHighlighting && !this.highlightStuckObjects)
            {
                this.highlightDirty = true;
            }

            this.wasHighlighting = this.highlightStuckObjects;

            this.despawnAll = settings.despawnAll;
            this.despawnCommercialVehicles = settings.despawnCommercialVehicles;
            this.despawnPedestrians = settings.despawnPedestrians;
            this.despawnPersonalVehicles = settings.despawnPersonalVehicles;
            this.despawnPublicTransit = settings.despawnPublicTransit;
            this.despawnServiceVehicles = settings.despawnServiceVehicles;
            this.despawnTaxis = settings.despawnTaxis;
        }

        private void cleanupAfterDisable()
        {
            NativeArray<Entity> cleanupEntities = this.stuckObjectQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < cleanupEntities.Length; ++i)
            {
                if (EntityManager.HasComponent<Highlighted>(cleanupEntities[i]))
                {
                    EntityManager.RemoveComponent<Highlighted>(cleanupEntities[i]);
                    EntityManager.AddComponent<BatchesUpdated>(cleanupEntities[i]);
                }

                if (EntityManager.HasComponent<UnstuckObject>(cleanupEntities[i]))
                {
                    EntityManager.RemoveComponent<UnstuckObject>(cleanupEntities[i]);
                    EntityManager.AddComponent<Updated>(cleanupEntities[i]);
                }

                if (EntityManager.HasComponent<StuckObject>(cleanupEntities[i]))
                {
                    EntityManager.RemoveComponent<StuckObject>(cleanupEntities[i]);
                    EntityManager.AddComponent<Updated>(cleanupEntities[i]);
                }
            }
        }
    }
}
