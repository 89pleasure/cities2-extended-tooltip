﻿using Colossal.Entities;
using Colossal.Mathematics;
using ExtendedTooltip.Models;
using ExtendedTooltip.Systems;
using ExtendedTooltip.Utils;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Economy;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Game.UI.InGame;
using Game.UI.Tooltip;
using Game.Zones;
using Gooee.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ExtendedTooltip.TooltipBuilder
{
    public class SpawnablesTooltipBuilder : TooltipBuilderBase
    {
        private readonly PrefabSystem m_PrefabSystem;
        private readonly Type m_PloppableBuildingDataType;
        private readonly Type m_HistoricalType;

        public SpawnablesTooltipBuilder(EntityManager entityManager, CustomTranslationSystem customTranslationSystem, PrefabSystem prefabSystem)
        : base(entityManager, customTranslationSystem)
        {
            m_PrefabSystem = prefabSystem;
            Assembly findStuffAssembly = m_ExtendedTooltipSystem.loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "FindStuff");
            m_PloppableBuildingDataType = findStuffAssembly?.GetTypes().FirstOrDefault(a => a.Name == "PloppableBuildingData");
            m_HistoricalType = findStuffAssembly?.GetTypes().FirstOrDefault(a => a.Name == "Historical");
            Mod.DebugLog($"Created SpawnablesTooltipBuilder.");
        }

        public void Build(ToolBaseSystem activeTool, bool IsMixed, Entity entity, Entity prefab, int buildingLevel, int currentCondition, int levelingCost, SpawnableBuildingData spawnableBuildingData, CitizenHappinessParameterData citizenHappinessParameters, TooltipGroup tooltipGroup, TooltipGroup secondaryTooltipGroup)
        {
            var model = m_ExtendedTooltipSystem.Model;

            if (model.ShowGrowablesHousehold == false && model.ShowGrowablesHouseholdDetails == false && model.ShowGrowablesLevel == false && model.ShowGrowablesLevelDetails == false && model.ShowGrowablesRent == false)
                return;

            bool isBulldozing = activeTool is BulldozeToolSystem;

            if (model.ShowGrowablesZoneInfo)
            {
                ZoneData zoneData = m_EntityManager.GetComponentData<ZoneData>(spawnableBuildingData.m_ZonePrefab);
                string rawZoneName = m_PrefabSystem.GetPrefab<PrefabBase>(spawnableBuildingData.m_ZonePrefab).name;
                string finalZoneName;

                // Offices and industrial zones have a different naming convention
                if (zoneData.m_AreaType == AreaType.Industrial) // Offices are technically industrial zones
                {
                    finalZoneName = m_CustomTranslationSystem.GetLocalGameTranslation($"Assets.NAME[{rawZoneName}]", rawZoneName);
                } else {
                    // Split the string into individual values
                    List<string> zoneInfos = [.. rawZoneName.Split(' ')];
                    for (int i = 0; i < zoneInfos.Count; i++)
                    {
                        zoneInfos[i] = m_CustomTranslationSystem.GetTranslation($"zone_info[{zoneInfos[i]}]", zoneInfos[i]);
                    }
                    finalZoneName = string.Join(", ", zoneInfos);
                }
                
                StringTooltip zoneTooltip = new()
                {
                    icon = "Media/Game/Icons/Zones.svg",
                    value = finalZoneName,
                };
                (model.UseExtendedLayout ? secondaryTooltipGroup : tooltipGroup).children.Add(zoneTooltip);
            }

            if (model.ShowGrowablesPloppableRICOInfo && m_PloppableBuildingDataType != null && TryGetPloppableRICOBuilding(entity, out object _))
            {
                string ricoString = "Plopped RICO";
                StringTooltip ploppableRicoTooltip = new()
                {
                    icon = "Media/Game/Icons/Zones.svg",
                    color = TooltipColor.Info
                };

                if (IsHistorical(entity))
                {
                    ricoString += $" [{m_CustomTranslationSystem.GetTranslation("", "Historical")}]";
                    ploppableRicoTooltip.color = TooltipColor.Warning;
                }

                ploppableRicoTooltip.value = ricoString;

                (model.UseExtendedLayout ? secondaryTooltipGroup : tooltipGroup).children.Add(ploppableRicoTooltip);
            }

            if (model.ShowGrowablesLevel)
            {
                bool isHistorical = m_PloppableBuildingDataType != null ? IsHistorical(entity) : false;
                TooltipColor buildingLevelColor = TooltipColor.Info;
                string buildingLevelLabel = m_CustomTranslationSystem.GetLocalGameTranslation("SelectedInfoPanel.LEVEL", "Level");
                string buildingLevelValue = !isHistorical ? $"{buildingLevel}/5" : $"{buildingLevel}";
                
                if (buildingLevel == 5)
                {
                    buildingLevelColor = TooltipColor.Success;
                }

                if (currentCondition < 0 && !isHistorical)
                {
                    buildingLevelColor = TooltipColor.Error;
                }

                if (model.ShowGrowablesLevelDetails == true && !isHistorical && levelingCost > 0)
                {
                    buildingLevelValue += $" [¢{currentCondition} / ¢{levelingCost}]";
                }

                StringTooltip levelTooltip = new()
                {
                    icon = "Media/Game/Icons/BuildingLevel.svg",
                    value = $"{buildingLevelLabel}: {buildingLevelValue}",
                    color = buildingLevelColor
                };
                (model.UseExtendedLayout && IsMixed ? secondaryTooltipGroup : tooltipGroup).children.Add(levelTooltip);
            }

            // LAND VALUE TOOLTIP
            if (model.ShowLandValue && !isBulldozing && m_EntityManager.TryGetComponent(entity, out Building building) && m_EntityManager.HasComponent<BuildingCondition>(entity))
            {
                if (building.m_RoadEdge != Entity.Null)
                {
                    LandValue landValue = m_EntityManager.GetComponentData<LandValue>(building.m_RoadEdge);
                    double landValueAmount = landValue.m_LandValue;
                    TooltipColor landValueTooltipColor = landValueAmount <= 150 ? TooltipColor.Success : landValueAmount <= 300 ? TooltipColor.Info : landValueAmount <= 450 ? TooltipColor.Warning : TooltipColor.Error;

                    string landValueLabel = m_CustomTranslationSystem.GetLocalGameTranslation("Infoviews.INFOVIEW[LandValue]", "Land Value");
                    string landValueString = landValueAmount.ToString("C0");
                    string finalLandValueString = $"{landValueLabel}: {landValueString}";

                    StringTooltip landValueTooltip = new()
                    {
                        value = finalLandValueString,
                        icon = "Media/Game/Icons/LandValue.svg",
                        color = landValueTooltipColor
                    };
                    (model.UseExtendedLayout && IsMixed ? secondaryTooltipGroup : tooltipGroup).children.Add(landValueTooltip);
                }
            }

            // Add residential info if available
            int residentCount = 0;
            int householdCount = 0;
            int maxHouseholds = 0;
            int petsCount = 0;
            List<int> householdRents = [];
            List<int> householdBalances = [];
            NativeList<Entity> householdsResult = new(Allocator.Temp);
            if ((model.ShowGrowablesHousehold == true || model.ShowGrowablesRent == true) && CreateTooltipsForResidentialProperties(ref residentCount, ref householdCount, ref maxHouseholds, ref petsCount, ref householdRents, ref householdBalances, ref householdsResult, entity, prefab))
            {
                BuildHouseholdCitizenInfo(model, activeTool, householdCount, maxHouseholds, residentCount, petsCount, out string finalInfoString);
                if (model.ShowGrowablesHousehold)
                {
                    int householdCapacityPercentage = householdCount == 0 ? 0 : (int)math.round(100 * householdCount / maxHouseholds);
                    TooltipColor householdTooltipColor = (householdCount == 0 || householdCapacityPercentage < 50) ? TooltipColor.Error : householdCapacityPercentage < 80 ? TooltipColor.Warning : TooltipColor.Success;
                    StringTooltip householdTooltip = new()
                    {
                        icon = "Media/Game/Icons/Household.svg",
                        value = finalInfoString,
                        color = householdTooltipColor
                    };
                    (model.UseExtendedLayout && IsMixed ? secondaryTooltipGroup : tooltipGroup).children.Add(householdTooltip);
                }

                if (model.ShowGrowablesHouseholdWealth && activeTool is DefaultToolSystem && householdsResult.Length > 0)
                {
                    HouseholdWealthKey wealthKey = householdsResult.Length == 1
                        ? CitizenUIUtils.GetHouseholdWealth(m_EntityManager, householdsResult[0], citizenHappinessParameters)
                        : CitizenUIUtils.GetAverageHouseholdWealth(m_EntityManager, householdsResult, citizenHappinessParameters);

                    TooltipColor tooltipColor = wealthKey switch
                    {
                        HouseholdWealthKey.Wretched => TooltipColor.Error,
                        HouseholdWealthKey.Poor => TooltipColor.Warning,
                        HouseholdWealthKey.Modest => TooltipColor.Info,
                        _ => TooltipColor.Success,
                    };

                    string wealthLabel = m_CustomTranslationSystem.GetLocalGameTranslation(householdsResult.Length > 1 ? "SelectedInfoPanel.AVERAGE_HOUSEHOLD_WEALTH" : "StatisticsPanel.STAT_TITLE[Wealth]", "Household wealth");
                    string wealthValue = m_CustomTranslationSystem.GetLocalGameTranslation($"SelectedInfoPanel.CITIZEN_WEALTH[{wealthKey}]");
                    StringTooltip wealthTooltip = new()
                    {
                        icon = "Media/Game/Icons/CitizenWealth.svg",
                        value = $"{wealthLabel}: {wealthValue}",
                        color = tooltipColor,
                    };
                    (model.UseExtendedLayout ? secondaryTooltipGroup : tooltipGroup).children.Add(wealthTooltip);
                }

                if (model.ShowGrowablesBalance && !isBulldozing && householdBalances.Count > 0)
                {
                    string balanceLabel = m_CustomTranslationSystem.GetTranslation("balance", "Balance");
                    string balanceValue;
                    int finalBalance = default;
                    int minBalance = default;
                    int maxBalance = default;

                    if (householdCount > 1)
                    {
                        minBalance = householdBalances.Min();
                        int minBalanceValue = minBalance < 0 ? Math.Abs(minBalance) : minBalance;
                        maxBalance = householdBalances.Max();
                        int maxBalanceValue = maxBalance < 0 ? Math.Abs(maxBalance) : maxBalance;
                        balanceValue = m_CustomTranslationSystem.GetTranslation("common.range_minmax_value_money", "", "SIGN0", minBalance < 0 ? "-" : "", "VALUE0", minBalanceValue.ToString(), "SIGN1", maxBalance < 0 ? "-" : "", "VALUE1", maxBalanceValue.ToString());
                    } else
                    {
                        finalBalance = householdBalances.First();
                        int finalRentValue = finalBalance < 0 ? Math.Abs(finalBalance) : finalBalance;
                        balanceValue = m_CustomTranslationSystem.GetLocalGameTranslation("Common.VALUE_MONEY", "", "SIGN", finalBalance < 0 ? "-" : "", "VALUE", finalRentValue.ToString());
                    }

                    StringTooltip balanceTooltip = new()
                    {
                        icon = "Media/Game/Icons/Money.svg",
                        value = $"{balanceLabel}: {balanceValue}",
                        color = (householdCount > 1 && minBalance < 0 && maxBalance < 0) || (householdCount == 1 && finalBalance < 0) ? TooltipColor.Error : TooltipColor.Info,
                    };

                    (model.UseExtendedLayout ? secondaryTooltipGroup : tooltipGroup).children.Add(balanceTooltip);
                }

                if (model.ShowGrowablesRent == true && !isBulldozing && householdRents.Count > 0)
                {
                    string rentLabel = m_CustomTranslationSystem.GetTranslation("rent", "Rent");
                    string rentValue;

                    // Only show range if there are more than 1 household and the min and max rents differ
                    if (householdCount > 1 && householdRents.Min() != householdRents.Max())
                    {
                        int minRent = householdRents.Min();
                        int minRentValue = minRent < 0 ? Math.Abs(minRent) : minRent;
                        int maxRent = householdRents.Max();
                        int maxRentValue = maxRent < 0 ? Math.Abs(maxRent) : maxRent;
                        rentValue = m_CustomTranslationSystem.GetTranslation("common.range_value_money", "", "SIGN0", minRent < 0 ? "-" : "", "VALUE0", minRentValue.ToString(), "SIGN1", maxRent < 0 ? "-" : "", "VALUE1", maxRentValue.ToString());
                    }
                    else
                    {
                        int finalRent = householdRents.First();
                        int finalRentValue = finalRent < 0 ? Math.Abs(finalRent) : finalRent;
                        rentValue = m_CustomTranslationSystem.GetLocalGameTranslation("Common.VALUE_MONEY_PER_MONTH", "-", "SIGN", finalRent < 0 ? "-" : "", "VALUE", finalRentValue.ToString());
                    }

                    StringTooltip rentTooltip = new()
                    {
                        icon = "Media/Game/Icons/Money.svg",
                        value = $"{rentLabel}: {rentValue}",
                        color = TooltipColor.Info,
                    };

                    (model.UseExtendedLayout ? secondaryTooltipGroup : tooltipGroup).children.Add(rentTooltip);
                }
            }
        }

        private bool CreateTooltipsForResidentialProperties(
            ref int residentCount,
            ref int householdCount,
            ref int maxHouseholds,
            ref int petsCount,
            ref List<int> householdRents,
            ref List<int> householdBalances,
            ref NativeList<Entity> householdsResult,
            Entity entity,
            Entity prefab
        ) {
            bool flag = false;
            bool isAbondened = m_EntityManager.HasComponent<Abandoned>(entity);
            bool hasBuildingPropertyData = m_EntityManager.TryGetComponent(prefab, out BuildingPropertyData buildingPropertyData);
            bool hasResidentialProperties = buildingPropertyData.m_ResidentialProperties > 0;
            RandomSeed randomSeed = RandomSeed.Next();

            // Only check for residents if the building is not abandoned and has residential properties
            if (!isAbondened && hasBuildingPropertyData && hasResidentialProperties)
            {
                flag = true;
                maxHouseholds += buildingPropertyData.m_ResidentialProperties;

                if (m_EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<Renter> renterBuffer))
                {
                    for (int i = 0; i < renterBuffer.Length; i++)
                    {
                        Entity renter = renterBuffer[i].m_Renter;

                        // Does only count residential properties rent
                        if (!m_EntityManager.HasComponent<Household>(renter))
                            continue;

                        // Get rent
                        int rent;
                        if (m_EntityManager.TryGetComponent(renter, out PropertyRenter propertyRenter))
                        {
                            Unity.Mathematics.Random random = randomSeed.GetRandom(1 + i);
                            rent = MathUtils.RoundToIntRandom(ref random, propertyRenter.m_Rent * 1f);
                            householdRents.Add(rent);
                        }

                        // Balances (Savings)
                        if (m_EntityManager.TryGetBuffer(renter, true, out DynamicBuffer<Resources> resources))
                        {
                            int balance = EconomyUtils.GetResources(Resource.Money, resources);
                            householdBalances.Add(balance);
                        }

                        // Household info
                        if (m_EntityManager.TryGetBuffer(renter, true, out DynamicBuffer<HouseholdCitizen> householdCitizenBuffer))
                        {
                            householdsResult.Add(renter);
                            householdCount++;
                            for (int j = 0; j < householdCitizenBuffer.Length; j++)
                            {
                                // Is not in death routine
                                if (!CitizenUtils.IsCorpsePickedByHearse(m_EntityManager, householdCitizenBuffer[j].m_Citizen))
                                {
                                    residentCount++;
                                }
                            }

                            // Woof
                            if (m_EntityManager.TryGetBuffer(renter, true, out DynamicBuffer<HouseholdAnimal> animalBuffer))
                            {
                                petsCount += animalBuffer.Length;
                            }
                        }
                    }
                }
            }

            return flag;
        }

        public void BuildHouseholdCitizenInfo(ExtendedTooltipModel model, ToolBaseSystem activeTool, int households, int maxHouseholds, int residents, int pets, out string finalInfoString)
        {
            // Households
            string householdsLabel = m_CustomTranslationSystem.GetLocalGameTranslation("SelectedInfoPanel.HOUSEHOLDS", "Households");
            string householdsValue = $"{households}/{maxHouseholds}";

            // Residents
            string residentsLabel = m_CustomTranslationSystem.GetLocalGameTranslation("Properties.ADJUST_HAPPINESS_MODIFIER_TARGET[Residents]", "Residents");
            string residentsValue = residents.ToString();

            // Pets
            string petsValue = pets.ToString();
            string petsLabel = pets > 1 ? m_CustomTranslationSystem.GetTranslation("pets", "Pets") : m_CustomTranslationSystem.GetTranslation("pet");

            // If residential building is > low density (only 1 household) show the household label only
            if (maxHouseholds > 1)
            {
                if (model.ShowGrowablesHouseholdDetails && activeTool is DefaultToolSystem)
                {
                    if (pets > 0)
                    {
                        finalInfoString = $"{householdsValue} {householdsLabel} [{residentsValue} {residentsLabel}, {petsValue} {petsLabel}]";
                        return;
                    } else
                    {
                        finalInfoString = $"{householdsValue} {householdsLabel} [{residentsValue} {residentsLabel}]";
                    }   
                }
                else
                {
                    finalInfoString = $"{householdsValue} {householdsLabel}";
                }
            }
            else // low densitiy housing only has 1 household
            {
                if (model.ShowGrowablesHouseholdDetails && activeTool is DefaultToolSystem && pets > 0)
                {
                    finalInfoString = $"{residentsValue} {residentsLabel} [{petsValue} {petsLabel}]";
                } else
                {
                    finalInfoString = $"{residentsValue} {residentsLabel}";
                }
            }
        }

        private bool TryGetPloppableRICOBuilding(Entity entity, out object ploppableRicoBuilding)
        {
            ploppableRicoBuilding = default;
            if (m_PloppableBuildingDataType != null && m_EntityManager.HasComponent(entity, m_PloppableBuildingDataType))
            {
                try
                {
                    ploppableRicoBuilding = EntityManagerUtils.GetComponentDynamic(m_EntityManager, entity, m_PloppableBuildingDataType);
                    return true;
                } catch (Exception e)
                {
                    Mod.DebugLog(e.Message);
                }
            }

            return false;
        }

        private bool IsHistorical(Entity entity)
        {
            if (m_PloppableBuildingDataType == null)
                return false;

            return m_EntityManager.HasComponent(entity, m_HistoricalType);
        }
    }
}
