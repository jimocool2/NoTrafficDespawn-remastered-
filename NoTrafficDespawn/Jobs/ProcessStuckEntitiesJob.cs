using Game.Common;
using Game.Creatures;
using Game.Pathfind;
using Game.Tools;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace NoTrafficDespawn
{
    [BurstCompile]
    public struct ProcessStuckEntitiesJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle m_EntityType;
        public ComponentTypeHandle<StuckObject> m_StuckObjectType;

        [ReadOnly] public ComponentLookup<Blocker> m_BlockerData;
        [ReadOnly] public ComponentLookup<DeliveryTruck> m_DeliveryTruckData;
        [ReadOnly] public ComponentLookup<Creature> m_CreatureData;
        [ReadOnly] public ComponentLookup<PersonalCar> m_PersonalCarData;
        [ReadOnly] public ComponentLookup<PassengerTransport> m_PassengerTransportData;
        [ReadOnly] public ComponentLookup<Bicycle> m_BicycleData;
        [ReadOnly] public ComponentLookup<Taxi> m_TaxiData;
        public ComponentLookup<PathOwner> m_PathOwnerData;

        public EntityCommandBuffer commandBuffer;
        public NativeReference<int> availableRemovalCount;

        public DespawnBehavior despawnBehavior;
        public bool highlightStuckObjects;
        public bool isDespawnFrame;
        public bool despawnAll;
        public bool despawnCommercialVehicles;
        public bool despawnPedestrians;
        public bool despawnPersonalVehicles;
        public bool despawnPublicTransit;
        public bool despawnBicycles;
        public bool despawnServiceVehicles;
        public bool despawnTaxis;
        public int deadlockLingerFrames;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
            NativeArray<StuckObject> stuckObjects = chunk.GetNativeArray(ref m_StuckObjectType);

            bool canDespawn = despawnBehavior != DespawnBehavior.NoDespawn;

            NativeBitArray handledIndices = new NativeBitArray(entities.Length, Allocator.Temp);
            NativeList<int> candidates = new NativeList<int>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                // Entity is no longer blocked — remove stuck state and move on.
                if (!m_BlockerData.HasComponent(entity))
                {
                    commandBuffer.RemoveComponent<StuckObject>(entity);
                    if (highlightStuckObjects)
                    {
                        commandBuffer.RemoveComponent<Highlighted>(entity);
                        commandBuffer.AddComponent<BatchesUpdated>(entity);
                    }
                    commandBuffer.AddComponent<Updated>(entity);

                    // Mark so the highlight pass does not touch this index.
                    handledIndices.Set(i, true);
                    continue;
                }

                if (!canDespawn)
                    continue; // NoDespawn mode: only highlighting is done (pass 3).

                // Increment and write back so the comparer sees the updated value.
                StuckObject stuck = stuckObjects[i];
                stuck.frameCount += 4;
                stuckObjects[i] = stuck;

                // Only bother collecting candidates on frames where we will actually despawn.
                if (isDespawnFrame &&
                    stuck.frameCount >= deadlockLingerFrames &&
                    availableRemovalCount.Value > 0 &&
                    ShouldDespawn(entity) &&
                    m_PathOwnerData.HasComponent(entity))
                {
                    candidates.Add(i);
                }
            }

            if (isDespawnFrame && candidates.Length > 0)
            {
                candidates.Sort(new FrameCountDescendingComparer { stuckObjects = stuckObjects });

                for (int j = 0; j < candidates.Length; j++)
                {
                    if (availableRemovalCount.Value <= 0)
                        break;

                    int i = candidates[j];
                    Entity entity = entities[i];

                    PathOwner pathOwner = m_PathOwnerData[entity];
                    pathOwner.m_State |= PathFlags.Stuck;
                    m_PathOwnerData[entity] = pathOwner;
                    commandBuffer.AddComponent<Updated>(entity);

                    availableRemovalCount.Value--;
                    handledIndices.Set(i, true); // skip highlight for this entity
                }
            }

            if (highlightStuckObjects)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!handledIndices.IsSet(i))
                    {
                        commandBuffer.AddComponent<Highlighted>(entities[i]);
                        commandBuffer.AddComponent<BatchesUpdated>(entities[i]);
                    }
                }
            }

            candidates.Dispose();
            handledIndices.Dispose();
        }

        private bool ShouldDespawn(Entity entity)
        {
            if (despawnAll) return true;

            bool isCreature = m_CreatureData.HasComponent(entity);
            bool isPersonalCar = m_PersonalCarData.HasComponent(entity);
            bool isTaxi = m_TaxiData.HasComponent(entity);
            bool isDeliveryTruck = m_DeliveryTruckData.HasComponent(entity);
            bool isPassengerTransport = m_PassengerTransportData.HasComponent(entity);
            bool isBicycle = m_BicycleData.HasComponent(entity);

            if (despawnPedestrians && isCreature) return true;
            if (despawnCommercialVehicles && isDeliveryTruck) return true;
            if (despawnPersonalVehicles && isPersonalCar) return true;
            if (despawnBicycles && isBicycle) return true;
            if (despawnTaxis && isTaxi) return true;
            if (despawnPublicTransit && isPassengerTransport) return true;

            if (despawnServiceVehicles &&
                !isCreature &&
                !isDeliveryTruck &&
                !isPersonalCar &&
                !isBicycle &&
                !isTaxi &&
                !isPassengerTransport)
                return true;

            return false;
        }
    }
}