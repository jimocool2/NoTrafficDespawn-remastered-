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

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                // No longer blocked — clean it up
                if (!m_BlockerData.HasComponent(entity))
                {
                    commandBuffer.RemoveComponent<StuckObject>(entity);
                    if (highlightStuckObjects)
                    {
                        commandBuffer.RemoveComponent<Highlighted>(entity);
                        commandBuffer.AddComponent<BatchesUpdated>(entity);
                    }
                    commandBuffer.AddComponent<Updated>(entity);
                    continue;
                }

                StuckObject stuck = stuckObjects[i];

                if (despawnBehavior != DespawnBehavior.NoDespawn)
                {
                    stuck.frameCount += 4;

                    if (stuck.frameCount >= deadlockLingerFrames && availableRemovalCount.Value > 0 && ShouldDespawn(entity))
                    {
                        if (m_PathOwnerData.TryGetComponent(entity, out PathOwner pathOwner))
                        {
                            pathOwner.m_State |= PathFlags.Stuck;
                            m_PathOwnerData[entity] = pathOwner;
                            commandBuffer.AddComponent<Updated>(entity);
                            availableRemovalCount.Value--;
                            continue; // being despawned, skip highlight update
                        }
                    }

                    stuckObjects[i] = stuck;
                }

                if (highlightStuckObjects)
                {
                    commandBuffer.AddComponent<Highlighted>(entity);
                    commandBuffer.AddComponent<BatchesUpdated>(entity);
                }
            }
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
                !isPassengerTransport
                ) return true;

            return false;
        }
    }
}