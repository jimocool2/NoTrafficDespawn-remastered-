using Game;
using Game.Buildings;
using Game.Common;
using Game.Creatures;
using Game.Pathfind;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using NoTrafficDespawn.Components;
using NoTrafficDespawn.Jobs;
using Unity.Collections;
using Unity.Entities;

namespace NoTrafficDespawn.Systems
{
    public partial class DisableTrafficDespawnSystem : GameSystemBase
    {
        private StuckMovingObjectSystem m_StuckMovingObjectSystem;
        private SimulationSystem m_SimulationSystem;
        private EntityCommandBufferSystem m_EntityCommandBufferSystem;
        private EntityQuery m_StuckObjectQuery;
        private EntityQuery m_UnstuckObjectQuery;

        private bool m_HighlightDirty;
        private bool m_WasHighlighting;
        private bool m_DespawnAll;
        private bool m_DespawnCommercialVehicles;
        private bool m_DespawnPedestrians;
        private bool m_DespawnPersonalVehicles;
        private bool m_DespawnPublicTransit;
        private bool m_DespawnBicycles;
        private bool m_DespawnServiceVehicles;
        private bool m_DespawnTaxis;

        public DespawnBehavior despawnBehavior;
        public bool highlightStuckObjects;
        public int deadlockLingerFrames;
        public int deadlockSearchDepth;
        public int maxStuckObjectRemovalCount;
        public int maxStuckObjectSpeed;
        public bool DespawnAll => this.m_DespawnAll;
        public bool DespawnPublicTransit => this.m_DespawnPublicTransit;

        private bool m_ShouldDisable;
        private int m_DespawnIntervalTicks;
        private int m_TicksSinceLastDespawn;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 4;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            this.m_StuckMovingObjectSystem = World.GetOrCreateSystemManaged<StuckMovingObjectSystem>();
            this.m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_EntityCommandBufferSystem = World.GetOrCreateSystemManaged<ModificationBarrier1>();

            Mod.Instance.settings.onSettingsApplied += settings =>
            {
                if (settings is TrafficDespawnSettings despawnSettings)
                {
                    this.updateSettings(despawnSettings);
                }
            };

            this.updateSettings(Mod.Instance.settings);

            this.m_UnstuckObjectQuery = GetEntityQuery(new EntityQueryDesc
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

            this.m_StuckObjectQuery = GetEntityQuery(new EntityQueryDesc
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

            RequireForUpdate(this.m_StuckObjectQuery);
        }

        protected override void OnUpdate()
        {
            if (this.m_SimulationSystem.selectedSpeed <= 0)
                return;

            if (this.m_ShouldDisable)
            {
                this.cleanupAfterDisable();
                this.Enabled = false;
                return;
            }

            // --- Main thread: highlightDirty cleanup (infrequent) ---
            if (m_HighlightDirty)
            {
                NativeArray<Entity> allStuck = this.m_StuckObjectQuery.ToEntityArray(Allocator.Temp);
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
                m_HighlightDirty = false;
            }

            m_TicksSinceLastDespawn++;
            bool isDespawnFrame = m_TicksSinceLastDespawn >= m_DespawnIntervalTicks;
            if (isDespawnFrame)
                m_TicksSinceLastDespawn = 0;

            // --- Job: process stuck entities ---
            NativeReference<int> removalCount = new NativeReference<int>(
                this.maxStuckObjectRemovalCount, Allocator.TempJob);

            EntityCommandBuffer commandBuffer = this.m_EntityCommandBufferSystem.CreateCommandBuffer();

            ProcessStuckEntitiesJob job = new ProcessStuckEntitiesJob
            {
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_StuckObjectType = SystemAPI.GetComponentTypeHandle<StuckObject>(isReadOnly: false),
                m_BlockerData = SystemAPI.GetComponentLookup<Blocker>(isReadOnly: true),
                m_DeliveryTruckData = SystemAPI.GetComponentLookup<DeliveryTruck>(isReadOnly: true),
                m_CreatureData = SystemAPI.GetComponentLookup<Creature>(isReadOnly: true),
                m_PersonalCarData = SystemAPI.GetComponentLookup<PersonalCar>(isReadOnly: true),
                m_PassengerTransportData = SystemAPI.GetComponentLookup<PassengerTransport>(isReadOnly: true),
                m_BicycleData = SystemAPI.GetComponentLookup<Bicycle>(isReadOnly: true),
                m_TaxiData = SystemAPI.GetComponentLookup<Taxi>(isReadOnly: true),
                m_PathOwnerData = SystemAPI.GetComponentLookup<PathOwner>(isReadOnly: false),
                commandBuffer = commandBuffer,
                availableRemovalCount = removalCount,
                despawnBehavior = this.despawnBehavior,
                highlightStuckObjects = this.highlightStuckObjects,
                isDespawnFrame = isDespawnFrame,
                despawnAll = this.m_DespawnAll,
                despawnCommercialVehicles = this.m_DespawnCommercialVehicles,
                despawnPedestrians = this.m_DespawnPedestrians,
                despawnPersonalVehicles = this.m_DespawnPersonalVehicles,
                despawnPublicTransit = this.m_DespawnPublicTransit,
                despawnBicycles = this.m_DespawnBicycles,
                despawnServiceVehicles = this.m_DespawnServiceVehicles,
                despawnTaxis = this.m_DespawnTaxis,
                deadlockLingerFrames = this.deadlockLingerFrames,
            };

            base.Dependency = JobChunkExtensions.Schedule(job, this.m_StuckObjectQuery, base.Dependency);
            this.m_EntityCommandBufferSystem.AddJobHandleForProducer(base.Dependency);

            // NativeReference must be disposed after the job completes
            base.Dependency = removalCount.Dispose(base.Dependency);

            // --- Main thread: unstuck entity cleanup (infrequent) ---
            if (this.highlightStuckObjects)
            {
                NativeArray<Entity> unstuckEntities = this.m_UnstuckObjectQuery.ToEntityArray(Allocator.Temp);
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
            this.m_StuckMovingObjectSystem.Enabled = settings.despawnBehavior == DespawnBehavior.Vanilla;
            this.m_ShouldDisable = settings.despawnBehavior == DespawnBehavior.Vanilla;
            if (!this.m_ShouldDisable)
            {
                this.Enabled = true;
            }

            this.despawnBehavior = settings.despawnBehavior;
            this.highlightStuckObjects = settings.highlightStuckObjects;
            this.deadlockLingerFrames = settings.deadlockLingerFrames;
            this.deadlockSearchDepth = settings.deadlockSearchDepth;
            this.maxStuckObjectRemovalCount = settings.maxStuckObjectRemovalCount;
            this.maxStuckObjectSpeed = settings.maxStuckObjectSpeed;

            this.m_DespawnIntervalTicks = settings.despawnIntervalTicks < 1 ? 1 : settings.despawnIntervalTicks;
            this.m_TicksSinceLastDespawn = 0;

            if (this.m_WasHighlighting && !this.highlightStuckObjects)
            {
                this.m_HighlightDirty = true;
            }

            this.m_WasHighlighting = this.highlightStuckObjects;

            this.m_DespawnAll = settings.despawnAll;
            this.m_DespawnCommercialVehicles = settings.despawnCommercialVehicles;
            this.m_DespawnPedestrians = settings.despawnPedestrians;
            this.m_DespawnPersonalVehicles = settings.despawnPersonalVehicles;
            this.m_DespawnPublicTransit = settings.despawnPublicTransit;
            this.m_DespawnBicycles = settings.despawnBicycles;
            this.m_DespawnServiceVehicles = settings.despawnServiceVehicles;
            this.m_DespawnTaxis = settings.despawnTaxis;
        }

        private void cleanupAfterDisable()
        {
            NativeArray<Entity> cleanupEntities = this.m_StuckObjectQuery.ToEntityArray(Allocator.Temp);
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