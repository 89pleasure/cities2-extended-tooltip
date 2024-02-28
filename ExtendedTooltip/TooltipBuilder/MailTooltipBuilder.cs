using Colossal.Entities;
using ExtendedTooltip.Systems;
using Game.Buildings;
using Game.Prefabs;
using Game.UI.Tooltip;
using Unity.Entities;
using System.Globalization;
using Game.Economy;
using Game.Vehicles;
using System;

namespace ExtendedTooltip.TooltipBuilder
{
    public class MailTooltipBuilder : TooltipBuilderBase
    {
        public MailTooltipBuilder(EntityManager entityManager, CustomTranslationSystem customTranslationSystem)
        : base(entityManager, customTranslationSystem)
        {
            UnityEngine.Debug.Log($"Created MailTooltipBuilder.");
        }

        public void BuildForProducer(Entity selectedEntity, Entity prefab, TooltipGroup tooltipGroup)
        {

            // MAIL PRODUCER
            if (m_EntityManager.TryGetComponent(selectedEntity, out MailProducer producer))
            {
                var sending = producer.m_SendingMail;
                var receiving = producer.receivingMail;
                string sendingValue = sending.ToString("D", CultureInfo.InvariantCulture);
                string receivingValue = receiving.ToString("D", CultureInfo.InvariantCulture);
                TooltipColor mailColor = TooltipColor.Info;
                if (sending > 50 || receiving > 50) mailColor = TooltipColor.Warning;
                if (sending > 80 || receiving > 80) mailColor = TooltipColor.Error;
                StringTooltip mailTooltip = new()
                {
                    icon = "Media/Game/Icons/Journal.svg",
                    value = m_CustomTranslationSystem.GetTranslation("mail.tooltip",
                        $"Mail out: {sendingValue}, in: {receivingValue}",
                        "RECEIVING", receivingValue,
                        "SENDING", sendingValue),
                    color = mailColor,
                };
                tooltipGroup.children.Add(mailTooltip);
            }
        }

        public void BuildForMailService(Entity selectedEntity, Entity prefab, TooltipGroup tooltipGroup,
            bool showResourceStats = true, bool showVehicleStats = true,
            bool showStorageStats = true, bool showFunctionStats = true)
        {

            // count mail related resources for selected entity
            int localMail = 0, outgoingMail = 0, unsortedMail = 0;
            if (m_EntityManager.TryGetBuffer(selectedEntity, true, out DynamicBuffer<Resources> resources))
            {
                // Both ways work, keep it commented for documentation purpose
                // localMail = EconomyUtils.GetResources(Resource.LocalMail, resources);
                // outgoingMail = EconomyUtils.GetResources(Resource.OutgoingMail, resources);
                // unsortedMail = EconomyUtils.GetResources(Resource.UnsortedMail, resources);
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i].m_Resource == Resource.LocalMail)
                        localMail += resources[i].m_Amount;
                    if (resources[i].m_Resource == Resource.OutgoingMail)
                        outgoingMail += resources[i].m_Amount;
                    if (resources[i].m_Resource == Resource.UnsortedMail)
                        unsortedMail += resources[i].m_Amount;
                }
                if (showResourceStats && localMail != 0)
                {
                    tooltipGroup.children.Add(new StringTooltip()
                    {
                        icon = "Media/Game/Resources/LocalMail.svg",
                        value = m_CustomTranslationSystem.GetTranslation(
                            "mail.local", $"Local: {localMail}",
                            "LOCAL", localMail.ToString("D", CultureInfo.InvariantCulture)),
                        color = TooltipColor.Info,
                    });
                }
                if (showResourceStats && outgoingMail != 0)
                {
                    tooltipGroup.children.Add(new StringTooltip()
                    {
                        icon = "Media/Game/Resources/OutgoingMail.svg",
                        value = m_CustomTranslationSystem.GetTranslation(
                            "mail.outgoing", $"Outgoing: {outgoingMail}",
                            "OUTGOING", outgoingMail.ToString("D", CultureInfo.InvariantCulture)),
                        color = TooltipColor.Info,
                    });
                }
                if (showResourceStats && unsortedMail != 0)
                {
                    tooltipGroup.children.Add(new StringTooltip()
                    {
                        icon = "Media/Game/Resources/UnsortedMail.svg",
                        value = m_CustomTranslationSystem.GetTranslation(
                            "mail.unsorted", $"Unsorted: {unsortedMail}",
                            "UNSORTED", unsortedMail.ToString("D", CultureInfo.InvariantCulture)),
                        color = TooltipColor.Info,
                    });
                }
            }

            // POST FACILITY VANS & TRUCKS PLUS STORAGE PERCENTAGE
            if (m_EntityManager.TryGetComponent(selectedEntity, out Game.Buildings.PostFacility facility)
                && m_EntityManager.TryGetComponent(prefab, out PostFacilityData data))
            {

                // Storage for accumulated statistics
                PostFacilityData accumulated = data;
                // Process all upgrades/extensions and accumulate statistics
                if (m_EntityManager.TryGetBuffer(selectedEntity, true, out DynamicBuffer<InstalledUpgrade> buffer))
                {
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        var upgrade = m_EntityManager.GetComponentData<PrefabRef>(buffer[j].m_Upgrade).m_Prefab;
                        if (m_EntityManager.TryGetComponent(upgrade, out PostFacilityData subdata))
                        {
                            accumulated.Combine(subdata);
                        }
                    }
                }

                if (showStorageStats)
                {
                    // STORAGE USAGE PERCENTAGE
                    float percentStorage = 100f * (localMail + outgoingMail + unsortedMail) / accumulated.m_MailCapacity;
                    string percentStorageValue = ((int)Math.Round(percentStorage))
                        .ToString("D", CultureInfo.InvariantCulture);
                    var storageColor = TooltipColor.Info;
                    if (percentStorage > 50f) storageColor = TooltipColor.Warning;
                    if (percentStorage > 80f) storageColor = TooltipColor.Error;
                    tooltipGroup.children.Add(new StringTooltip()
                    {
                        icon = "Media/Game/Icons/PostService.svg",
                        value = m_CustomTranslationSystem.GetTranslation("mail.storage",
                            $"Storage: {percentStorageValue}%", "STORAGE", percentStorageValue),
                        color = storageColor
                    });
                }

                if (showVehicleStats)
                {
                    int vansUsed = 0, trucksUsed = 0;
                    // Process all owned vehicles to check which ones are doing work
                    if (m_EntityManager.TryGetBuffer(selectedEntity, true, out DynamicBuffer<OwnedVehicle> vehicles))
                    {
                        for (int j = 0; j < vehicles.Length; j++)
                        {
                            var vehicle = m_EntityManager.GetComponentData<PrefabRef>(vehicles[j].m_Vehicle).m_Prefab;
                            if (m_EntityManager.HasComponent<DeliveryTruckData>(vehicle)) trucksUsed += 1;
                            else if (m_EntityManager.HasComponent<PostVanData>(vehicle)) vansUsed += 1;
                        }
                    }
                    // VANS/TRUCKS USE/TOTAL (ToDo: also add guests?)
                    if (accumulated.m_PostVanCapacity > 0 || accumulated.m_PostTruckCapacity > 0)
                    {
                        string vansUsedValue = vansUsed.ToString("D", CultureInfo.InvariantCulture);
                        string trucksUsedValue = trucksUsed.ToString("D", CultureInfo.InvariantCulture);
                        string vansTotalValue = accumulated.m_PostVanCapacity.ToString("D", CultureInfo.InvariantCulture);
                        string trucksTotalValue = accumulated.m_PostTruckCapacity.ToString("D", CultureInfo.InvariantCulture);
                        string vansString = m_CustomTranslationSystem.GetTranslation(
                            "mail.vans", $"Vans: {vansUsedValue}/{vansTotalValue}",
                            "VANS_USED", vansUsedValue, "VANS_TOTAL", vansTotalValue);
                        string trucksString = m_CustomTranslationSystem.GetTranslation(
                            "mail.trucks", $"Trucks: {trucksUsedValue}/{trucksTotalValue}",
                            "TRUCKS_USED", trucksUsedValue, "TRUCKS_TOTAL", trucksTotalValue);
                        string vehiclesString;
                        // Only show relevant information
                        if (accumulated.m_PostVanCapacity == 0)
                        {
                            vehiclesString = trucksString;
                        }
                        else if (accumulated.m_PostTruckCapacity == 0)
                        {
                            vehiclesString = vansString;
                        }
                        else
                        {
                            vehiclesString = m_CustomTranslationSystem.GetTranslation("mail.vehicles",
                                $"{vansString}, {trucksString}", "VANS", vansString, "TRUCKS", trucksString);
                        }
                        tooltipGroup.children.Add(new StringTooltip()
                        {
                            icon = "Media/Game/Icons/DeliveryVan.svg",
                            value = vehiclesString,
                            color = TooltipColor.Info,
                        });

                    }
                }

                if (showFunctionStats)
                {
                    // SORTING USAGE PERCENTAGE
                    if (accumulated.m_SortingRate > 0)
                    {
                        string percentSortingValue = facility.m_ProcessingFactor
                            .ToString("D", CultureInfo.InvariantCulture);
                        tooltipGroup.children.Add(new StringTooltip()
                        {
                            icon = "Media/Game/Icons/AdvancedSorting.svg",
                            value = m_CustomTranslationSystem.GetTranslation("mail.sorting",
                                $"Sorting: {percentSortingValue}%", "SORTING", percentSortingValue),
                            color = TooltipColor.Info
                        });
                    }
                }

            }
        }

        public void BuildForMailBox(Entity selectedEntity, Entity prefab, TooltipGroup tooltipGroup)
        {
            // MAIL-BOX STORAGE PERCENTAGE
            if (m_EntityManager.TryGetComponent(prefab, out MailBoxData mailbox) &&
                m_EntityManager.TryGetComponent(selectedEntity, out Game.Routes.MailBox route))
            {
                float percentStorage = 100f * route.m_MailAmount / mailbox.m_MailCapacity;
                string percentStorageValue = ((int)Math.Round(percentStorage))
                    .ToString("D", CultureInfo.InvariantCulture);
                var storageColor = TooltipColor.Info;
                if (percentStorage > 50f) storageColor = TooltipColor.Warning;
                if (percentStorage > 80f) storageColor = TooltipColor.Error;
                tooltipGroup.children.Add(new StringTooltip()
                {
                    icon = "Media/Game/Icons/Mail Box.svg",
                    value = m_CustomTranslationSystem.GetTranslation("mail.mailbox",
                        $"Mail-Box: {percentStorageValue}%", "MAILBOX", percentStorageValue),
                    color = storageColor
                });
            }
        }

    }
}
