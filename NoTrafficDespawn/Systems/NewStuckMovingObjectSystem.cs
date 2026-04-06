using Game;
using Game.Common;
using Game.Creatures;
using Game.Pathfind;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using NoTrafficDespawn.Jobs;
using Unity.Entities;

namespace NoTrafficDespawn.Systems
{
    public partial class NewStuckMovingObjectSystem : GameSystemBase
    {
        private EntityQuery m_BlockedEntityQuery;
        private EntityCommandBufferSystem m_EntityCommandBufferSystem;
        private DisableTrafficDespawnSystem m_DisableTrafficDespawnSystem;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 4;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            this.m_EntityCommandBufferSystem = World.GetOrCreateSystemManaged<ModificationBarrier1>();
            this.m_DisableTrafficDespawnSystem = World.GetOrCreateSystemManaged<DisableTrafficDespawnSystem>();
            m_BlockedEntityQuery = GetEntityQuery(ComponentType.ReadOnly<Blocker>(), ComponentType.ReadOnly<UpdateFrame>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            RequireForUpdate(m_BlockedEntityQuery);
        }

        protected override void OnUpdate()
        {
            TagStuckObjectsJob stuckCheckJob = default;
            stuckCheckJob.highlightObjects = this.m_DisableTrafficDespawnSystem.highlightStuckObjects;
            stuckCheckJob.m_EntityType = SystemAPI.GetEntityTypeHandle();
            stuckCheckJob.m_BlockerType = SystemAPI.GetComponentTypeHandle<Blocker>(isReadOnly: true);
            stuckCheckJob.m_GroupMemberType = SystemAPI.GetComponentTypeHandle<GroupMember>(isReadOnly: true);
            stuckCheckJob.m_CurrentVehicleType = SystemAPI.GetComponentTypeHandle<CurrentVehicle>(isReadOnly: true);
            stuckCheckJob.m_RideNeederType = SystemAPI.GetComponentTypeHandle<RideNeeder>(isReadOnly: true);
            stuckCheckJob.m_TargetType = SystemAPI.GetComponentTypeHandle<Target>(isReadOnly: true);
            stuckCheckJob.m_BlockerData = SystemAPI.GetComponentLookup<Blocker>(isReadOnly: true);
            stuckCheckJob.m_ControllerData = SystemAPI.GetComponentLookup<Controller>(isReadOnly: true);
            stuckCheckJob.m_CurrentVehicleData = SystemAPI.GetComponentLookup<CurrentVehicle>(isReadOnly: true);
            stuckCheckJob.m_DispatchedData = SystemAPI.GetComponentLookup<Dispatched>(isReadOnly: true);
            stuckCheckJob.m_PathOwnerType = SystemAPI.GetComponentTypeHandle<PathOwner>();
            stuckCheckJob.m_AnimalCurrentLaneType = SystemAPI.GetComponentTypeHandle<AnimalCurrentLane>();
            stuckCheckJob.minStuckSpeed = (byte)this.m_DisableTrafficDespawnSystem.maxStuckObjectSpeed;
            stuckCheckJob.maxTraversalCount = this.m_DisableTrafficDespawnSystem.deadlockSearchDepth;
            stuckCheckJob.deadlocksOnly = this.m_DisableTrafficDespawnSystem.despawnBehavior == DespawnBehavior.DespawnDeadlocksOnly;
            stuckCheckJob.commandBuffer = this.m_EntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            base.Dependency = JobChunkExtensions.ScheduleParallel(stuckCheckJob, m_BlockedEntityQuery, base.Dependency);

            this.m_EntityCommandBufferSystem.AddJobHandleForProducer(base.Dependency);
        }
    }
}
