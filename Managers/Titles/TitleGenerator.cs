using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli.Managers.Titles
{
    // Gerador de títulos - cria títulos baseados em XML ou automaticamente
    public static class TitleGenerator
    {
        // Cria um título de reino
        internal static FeudalTitle CreateKingdom(Hero deJure, Kingdom faction, TitleType type, List<FeudalTitle> vassals, FeudalContract contract, string stringId = null, TextObject name = null, TextObject fullName = null)
        {
            try
            {
                if (deJure == null || faction == null || contract == null)
                    return null;

                var title = new FeudalTitle(type,
                    null,
                    vassals,
                    deJure,
                    faction.Leader,
                    name != null ? name : faction.Name,
                    contract,
                    stringId,
                    fullName);
                TitleManager.Instance.ExecuteAddTitle(title);
                TitleManager.Instance.Kingdoms[title] = faction;
                return title;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CreateKingdom: {ex.Message}"));
                return null;
            }
        }

        // Cria um título de império
        internal static FeudalTitle CreateEmpire(Hero deJure, Kingdom faction, List<FeudalTitle> vassals,
          FeudalContract contract, string stringId = null)
        {
            try
            {
                if (deJure == null || faction == null || contract == null || faction.Leader == null)
                    return null;

                var title = new FeudalTitle(TitleType.Empire, null, vassals, deJure, faction.Leader,
                    faction.Name, contract, stringId);
                TitleManager.Instance.ExecuteAddTitle(title);
                TitleManager.Instance.Kingdoms[title] = faction;
                return title;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CreateEmpire: {ex.Message}"));
                return null;
            }
        }

        // Funda um reino a partir de títulos existentes
        public static void FoundKingdom(TitleAction action, FeudalContract contract = null)
        {
            try
            {
                if (action == null || action.ActionTaker == null || action.ActionTaker.Clan == null || action.ActionTaker.Clan.Kingdom == null)
                    return;

                FeudalTitle newTitle = CreateKingdom(action.ActionTaker,
                    action.ActionTaker.Clan.Kingdom,
                    TitleType.Kingdom,
                    new List<FeudalTitle>(action.Vassals),
                    contract != null ? contract : action.Title.Contract);

                if (newTitle != null)
                {
                    action.Title.DriftTitle(newTitle, false);
                    foreach (var vassal in action.Vassals)
                    {
                        vassal.DriftTitle(newTitle);
                    }

                    TitleAction newAction = TitleManager.Instance.GetModel().GetFoundKingdom(action.ActionTaker.Clan.Kingdom,
                        action.ActionTaker);
                    newAction.SetTitle(newTitle);

                    TitleManager.Instance.CreateTitle(newAction);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in FoundKingdom: {ex.Message}"));
            }
        }

        // Funda um império
        public static void FoundEmpire(TitleAction action, TextObject factionName, string stringId = null, string contractType = null)
        {
            try
            {
                if (action == null || action.ActionTaker == null || action.ActionTaker.Clan == null || action.ActionTaker.Clan.Kingdom == null)
                    return;

                var kingdom = action.ActionTaker.Clan.Kingdom;
                kingdom.ChangeKingdomName(factionName, factionName);
                var newTitle = CreateEmpire(action.ActionTaker, kingdom, new List<FeudalTitle>(action.Vassals),
                    GenerateContract(contractType), stringId);

                if (newTitle != null)
                {
                    foreach (var vassal in action.Vassals)
                    {
                        vassal.DriftTitle(newTitle);
                    }

                    TitleAction newAction = TitleManager.Instance.GetModel().GetFoundEmpire(action.ActionTaker.Clan.Kingdom,
                        action.ActionTaker);
                    newAction.SetTitle(newTitle);

                    TitleManager.Instance.CreateTitle(newAction);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in FoundEmpire: {ex.Message}"));
            }
        }

        // Cria título sem terras (ducado, condado sem feudo direto)
        internal static FeudalTitle CreateUnlandedTitle(Hero deJure, TitleType type, List<FeudalTitle> vassals,
            TextObject name, FeudalContract contract, TextObject fullName = null)
        {
            var title = new FeudalTitle(type, null, vassals, deJure, deJure, name, contract, null, fullName);
            TitleManager.Instance.ExecuteAddTitle(title);
            return title;
        }

        // Cria título com terras (condado, baronia)
        internal static FeudalTitle CreateLandedTitle(Settlement settlement, Hero deJure, TitleType type,
            FeudalContract contract, List<FeudalTitle> vassals = null, TextObject fullName = null)
        {
            try
            {
                if (settlement == null || contract == null)
                    return null;

                var deFacto = settlement.OwnerClan?.Leader;
                if (deJure == null)
                {
                    deJure = settlement.Owner;
                }

                if (deFacto == null && deJure == null)
                    return null;

                if (vassals == null)
                {
                    vassals = new List<FeudalTitle>();
                }

                // Adicionar vilas ligadas como senhorios
                if (settlement.BoundVillages != null)
                {
                    foreach (var village in settlement.BoundVillages)
                    {
                        try
                        {
                            if (village?.Settlement != null)
                            {
                                var lordshipTitle = CreateLordship(village.Settlement, deJure, contract);
                                if (lordshipTitle != null)
                                {
                                    vassals.Add(lordshipTitle);
                                    TitleManager.Instance.ExecuteAddTitle(lordshipTitle);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error creating lordship: {ex.Message}"));
                        }
                    }
                }

                var title = new FeudalTitle(type, settlement, vassals, deJure, deFacto, settlement.Name, contract, settlement.StringId, fullName);
                TitleManager.Instance.ExecuteAddTitle(title);
                return title;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CreateLandedTitle: {ex.Message}"));
                return null;
            }
        }

        // Cria senhorio (vila)
        internal static FeudalTitle CreateLordship(Settlement settlement, Hero deJure, FeudalContract contract)
        {
            try
            {
                if (settlement == null || contract == null || settlement.Village == null || settlement.Village.Bound == null)
                    return null;

                var owner = settlement.Village.Bound.Owner;
                if (owner == null)
                    return null;

                return new FeudalTitle(TitleType.Lordship, settlement, null, deJure, owner, settlement.Name, contract, settlement.StringId);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CreateLordship: {ex.Message}"));
                return null;
            }
        }

        // Gera contrato padrão baseado no tipo
        internal static FeudalContract GenerateContract(string type)
        {
            var contract = type switch
            {
                "feudal_elective" => new FeudalContract(
                    DefaultGovernments.Instance.Feudal,
                    DefaultSuccessions.Instance.FeudalElective,
                    DefaultInheritances.Instance.Primogeniture,
                    DefaultGenderLaws.Instance.Agnatic),
                "imperial" => new FeudalContract(
                     DefaultGovernments.Instance.Feudal, DefaultSuccessions.Instance.FeudalElective,
                     DefaultInheritances.Instance.Primogeniture,
                     DefaultGenderLaws.Instance.Agnatic),
                "tribal" => new FeudalContract(
                     DefaultGovernments.Instance.Clan, DefaultSuccessions.Instance.FeudalElective,
                     DefaultInheritances.Instance.Primogeniture,
                     DefaultGenderLaws.Instance.Agnatic),
                "republic" => new FeudalContract(
                     DefaultGovernments.Instance.Republic, DefaultSuccessions.Instance.Republic,
                     DefaultInheritances.Instance.Primogeniture,
                     DefaultGenderLaws.Instance.Agnatic),
                _ => new FeudalContract(
                     DefaultGovernments.Instance.Feudal, DefaultSuccessions.Instance.Hereditary,
                     DefaultInheritances.Instance.Primogeniture,
                     DefaultGenderLaws.Instance.Agnatic)
            };

            return contract;
        }

        // Gera contrato com parâmetros específicos
        internal static FeudalContract GenerateContract(string government, string succession, string inheritance, string genderLaw)
        {
            return new FeudalContract(
                    DefaultGovernments.Instance.GetById(government),
                    DefaultSuccessions.Instance.GetById(succession),
                    DefaultInheritances.Instance.GetById(inheritance),
                    DefaultGenderLaws.Instance.GetById(genderLaw));
        }

        // Obtém herói pelo ID
        private static Hero GetDeJure(string heroId, Settlement settlement)
        {
            var target = Hero.AllAliveHeroes.FirstOrDefault(x => x.StringId == heroId);
            if (target == null)
            {
                var hero1Dead = Hero.DeadOrDisabledHeroes.FirstOrDefault(x => x.StringId == heroId);
                if (hero1Dead != null)
                {
                    var clan = hero1Dead.Clan;
                    if (!clan.IsEliminated)
                    {
                        target = clan.Leader;
                    }
                    else if (clan.Kingdom != null)
                    {
                        target = clan.Kingdom.Leader;
                    }
                }
            }

            if (target == null && settlement != null)
            {
                target = settlement.Owner;
            }

            return target;
        }
    }
}