using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli.Managers.Titles
{
    // Modelo de título - cálculos de custo, validações, etc.
    public class TitleModel
    {
        // Obter ação possível para um tipo
        public virtual TitleAction GetAction(ActionType type, FeudalTitle title, Hero taker, Hero receiver = null)
        {
            var action = new TitleAction(type, title, taker);

            switch (type)
            {
                case ActionType.Usurp:
                    action = GetUsurpAction(title, taker);
                    break;
                case ActionType.Claim:
                    action = GetClaimAction(title, taker);
                    break;
                case ActionType.Revoke:
                    action = GetRevokeAction(title, taker);
                    break;
                case ActionType.Create:
                    action = GetCreateAction(title, taker);
                    break;
                case ActionType.Grant:
                    action = GetGrantAction(title, taker, receiver);
                    break;
                case ActionType.Found:
                    action = GetFoundKingdom(null, taker);
                    break;
                case ActionType.Destroy:
                    action = GetDestroyAction(title, taker);
                    break;
            }

            return action;
        }

        // Fundar reino
        public virtual TitleAction GetFoundKingdom(Kingdom faction, Hero founder)
        {
            var action = new TitleAction(ActionType.Found, null, founder);
            action.Gold = 50000f;
            action.Influence = 200f;
            action.Renown = 500f;
            action.Possible = true;
            action.Reason = new TextObject("{=BKFoundKingdom}Found a new kingdom from your titles.");
            return action;
        }

        // Fundar império
        public virtual TitleAction GetFoundEmpire(Kingdom faction, Hero founder)
        {
            var action = new TitleAction(ActionType.Found, null, founder);
            action.Gold = 200000f;
            action.Influence = 500f;
            action.Renown = 2000f;
            action.Possible = true;
            action.Reason = new TextObject("{=BKFoundEmpire}Found a new empire from your kingdoms.");
            return action;
        }

        // Usurpar título
        public virtual TitleAction GetUsurpAction(FeudalTitle title, Hero taker)
        {
            var action = new TitleAction(ActionType.Usurp, title, taker);

            if (title == null || taker == null)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKInvalidTarget}Invalid title or claimant.");
                return action;
            }

            // Não pode usurpar próprio título
            if (title.deJure == taker)
            {
                action.Possible = false;
                action.Reason = new TextObject("Você já é o titular.");
                return action;
            }

            // Verificar se é título soberano
            if (title.IsSovereignLevel)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKSovereignTitle}Cannot usurp sovereign titles directly.");
                return action;
            }

            // Verificar claim válido OU controle de terra
            bool hasValidClaim = title.HeroHasValidClaim(taker);
            bool holdsLand = title.Fief != null && title.Fief.OwnerClan == taker.Clan;

            // Verificar suserano
            var suzerain = TitleManager.Instance.GetImmediateSuzerain(title);
            bool sameRealm = suzerain != null && suzerain.deJure != null && suzerain.deJure.Clan?.Kingdom == taker.Clan?.Kingdom;

            if (!hasValidClaim && !holdsLand)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKNoClaimOrLand}You need a valid claim or to hold the title's lands.");
                return action;
            }

            // Se mesmo reino, requer claim maduro (não apenas controle de terra)
            if (sameRealm && !hasValidClaim)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKNeedMatureClaim}Within the realm, you need a matured claim to usurp.");
                return action;
            }

            // Custos baseados no tipo de título
            action.Gold = GetGoldUsurpCost(title);
            action.Influence = GetInfluenceUsurpCost(title);
            action.Renown = GetRenownUsurpCost(title);
            action.Possible = true;

            if (hasValidClaim)
                action.Reason = new TextObject("{=BKUsurpClaim}Usurp {TITLE} using your claim.")
                    .SetTextVariable("TITLE", title.FullName);
            else
                action.Reason = new TextObject("{=BKUsurpLand}Usurp {TITLE} by right of conquest.")
                    .SetTextVariable("TITLE", title.FullName);

            CheckTitleCosts(action);
            return action;
        }

        // Fabricar claim
        public virtual TitleAction GetClaimAction(FeudalTitle title, Hero taker)
        {
            var action = new TitleAction(ActionType.Claim, title, taker);

            if (title == null || taker == null)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKInvalidTarget}Invalid title or claimant.");
                return action;
            }

            // Não pode claimar próprio título
            if (title.deJure == taker)
            {
                action.Possible = false;
                action.Reason = new TextObject("Você já é o titular.");
                return action;
            }

            // Não pode claimar se já tem claim
            if (title.GetHeroClaim(taker) != ClaimType.None)
            {
                action.Possible = false;
                action.Reason = new TextObject("Você já possui uma reivindicação madura ou em andamento.");
                return action;
            }

            // Custo baseado no nível do título
            action.Gold = GetGoldClaimCost(title);
            action.Influence = GetInfluenceClaimCost(title);
            action.Renown = GetRenownClaimCost(title);
            action.Possible = true;
            action.Reason = new TextObject("Fabricar uma reivindicação sobre {TITLE}. Prazo: um ano.")
                .SetTextVariable("TITLE", title.FullName);

            CheckTitleCosts(action);
            return action;
        }

        private static void CheckTitleCosts(TitleAction action)
        {
            var hero = action.ActionTaker;
            if (action.Title.deJure == null || hero.Clan == null)
            {
                action.Possible = false;
                action.Reason = new TextObject("O título precisa de um titular e o pretendente precisa pertencer a um clã.");
                return;
            }
            if (hero.Gold < action.Gold ||
                hero.Clan.Influence < action.Influence || hero.Clan.Renown < action.Renown)
            {
                action.Possible = false;
                action.Reason = new TextObject($"Recursos necessários: {action.Gold:0} de ouro, {action.Influence:0} de influência e {action.Renown:0} de renome.");
            }
        }

        // Revogar título
        public virtual TitleAction GetRevokeAction(FeudalTitle title, Hero taker)
        {
            var action = new TitleAction(ActionType.Revoke, title, taker);

            if (title == null || taker == null)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKInvalidTarget}Invalid title or revoker.");
                return action;
            }

            // Só pode revogar se for o suserano
            var suzerain = TitleManager.Instance.GetImmediateSuzerain(title);
            if (suzerain == null || suzerain.deJure != taker)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKNotSuzerain}You are not the liege of this title.");
                return action;
            }

            // Não pode revogar título soberano
            if (title.IsSovereignLevel)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKSovereignTitle}Cannot revoke sovereign titles.");
                return action;
            }

            action.Gold = 0f;
            action.Influence = GetInfluenceRevokeCost(title);
            action.Renown = 50f;
            action.Possible = true;
            action.Reason = new TextObject("{=BKRevokeTitle}Revoke {TITLE} from {VASSAL}.")
                .SetTextVariable("TITLE", title.FullName)
                .SetTextVariable("VASSAL", title.deJure.Name);

            return action;
        }

        // Criar título (quando não há dono legal)
        public virtual TitleAction GetCreateAction(FeudalTitle title, Hero taker)
        {
            var action = new TitleAction(ActionType.Create, title, taker);

            if (title == null || taker == null)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKInvalidTarget}Invalid title or founder.");
                return action;
            }

            // Só pode criar se não tem dono legal
            if (title.deJure != null)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKHasDeJure}This title already has a legal holder.");
                return action;
            }

            // Precisa ser o detentor de facto
            if (title.DeFactoHolder != taker)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKNotDeFacto}You do not hold this title de facto.");
                return action;
            }

            // Custos
            action.Gold = GetGoldCreateCost(title);
            action.Influence = GetInfluenceCreateCost(title);
            action.Renown = GetRenownCreateCost(title);
            action.Possible = true;
            action.Reason = new TextObject("{=BKCreateTitle}Create the title of {TITLE}.")
                .SetTextVariable("TITLE", title.FullName);

            return action;
        }

        // Conceder título
        public virtual TitleAction GetGrantAction(FeudalTitle title, Hero taker, Hero receiver)
        {
            var action = new TitleAction(ActionType.Grant, title, taker);

            if (title == null || taker == null || receiver == null)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKInvalidTarget}Invalid parameters.");
                return action;
            }

            // Só pode conceder se for o dono legal
            if (title.deJure != taker)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKNotOwner}You are not the legal holder of this title.");
                return action;
            }

            // Não pode conceder a si mesmo
            if (title.deJure == receiver)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKAlreadyHolder}This character already holds this title.");
                return action;
            }

            // Verificar se receiver é vassalo ou pode se tornar
            if (receiver.Clan.Kingdom != taker.Clan.Kingdom && !receiver.Clan.IsUnderMercenaryService)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKNotInRealm}Recipient must be in your realm.");
                return action;
            }

            action.Gold = 0f;
            action.Influence = GetInfluenceGrantCost(title);
            action.Renown = GetRenownGrantCost(title);
            action.Possible = true;
            action.Reason = new TextObject("{=BKGrantTitle}Grant {TITLE} to {RECEIVER}.")
                .SetTextVariable("TITLE", title.FullName)
                .SetTextVariable("RECEIVER", receiver.Name);

            return action;
        }

        // Destruir título
        public virtual TitleAction GetDestroyAction(FeudalTitle title, Hero taker)
        {
            var action = new TitleAction(ActionType.Destroy, title, taker);

            if (title == null || taker == null)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKInvalidTarget}Invalid title or destroyer.");
                return action;
            }

            // Só dono legal pode destruir
            if (title.deJure != taker)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKNotOwner}You are not the legal holder of this title.");
                return action;
            }

            // Não pode destruir título soberano
            if (title.IsSovereignLevel)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKSovereignTitle}Cannot destroy sovereign titles.");
                return action;
            }

            // Não pode destruir se tem vassalos
            if (title.Vassals != null && title.Vassals.Count > 0)
            {
                action.Possible = false;
                action.Reason = new TextObject("{=BKHasVassals}Title has vassals. Revoke them first.");
                return action;
            }

            action.Gold = 0f;
            action.Influence = 100f;
            action.Renown = 100f;
            action.Possible = true;
            action.Reason = new TextObject("{=BKDestroyTitle}Destroy the title of {TITLE}.")
                .SetTextVariable("TITLE", title.FullName);

            return action;
        }

        // Custos de usurpação
        public virtual float GetGoldUsurpCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 5000f,
                TitleType.County => 2000f,
                TitleType.Barony => 1000f,
                TitleType.Lordship => 500f,
                _ => 10000f
            };
        }

        public virtual float GetInfluenceUsurpCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 100f,
                TitleType.County => 50f,
                TitleType.Barony => 25f,
                TitleType.Lordship => 10f,
                _ => 200f
            };
        }

        public virtual float GetRenownUsurpCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 200f,
                TitleType.County => 100f,
                TitleType.Barony => 50f,
                TitleType.Lordship => 25f,
                _ => 500f
            };
        }

        // Custos de claim
        public virtual float GetGoldClaimCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 1000f,
                TitleType.County => 500f,
                TitleType.Barony => 250f,
                TitleType.Lordship => 100f,
                _ => 2000f
            };
        }

        public virtual float GetInfluenceClaimCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 50f,
                TitleType.County => 25f,
                TitleType.Barony => 10f,
                TitleType.Lordship => 5f,
                _ => 100f
            };
        }

        public virtual float GetRenownClaimCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 50f,
                TitleType.County => 25f,
                TitleType.Barony => 10f,
                TitleType.Lordship => 5f,
                _ => 100f
            };
        }

        // Custos de criação
        public virtual float GetGoldCreateCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 2000f,
                TitleType.County => 1000f,
                TitleType.Barony => 500f,
                TitleType.Lordship => 200f,
                _ => 5000f
            };
        }

        public virtual float GetInfluenceCreateCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 50f,
                TitleType.County => 25f,
                TitleType.Barony => 10f,
                TitleType.Lordship => 5f,
                _ => 100f
            };
        }

        public virtual float GetRenownCreateCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 100f,
                TitleType.County => 50f,
                TitleType.Barony => 25f,
                TitleType.Lordship => 10f,
                _ => 200f
            };
        }

        // Custos de concessão
        public virtual float GetInfluenceGrantCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 50f,
                TitleType.County => 25f,
                TitleType.Barony => 10f,
                TitleType.Lordship => 5f,
                _ => 100f
            };
        }

        public virtual float GetRenownGrantCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 50f,
                TitleType.County => 25f,
                TitleType.Barony => 10f,
                TitleType.Lordship => 5f,
                _ => 100f
            };
        }

        // Custos de revogação
        public virtual float GetInfluenceRevokeCost(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Dukedom => 100f,
                TitleType.County => 50f,
                TitleType.Barony => 25f,
                TitleType.Lordship => 10f,
                _ => 200f
            };
        }

        // Custo de cavalaria
        public virtual ExplainedNumber GetGrantKnighthoodCost(Hero grantor)
        {
            var explained = new ExplainedNumber(100f, true, new TextObject("{=BKKnightCost}Knighthood Cost"));

            if (grantor.Clan.Tier >= 5)
                explained.Add(-20f, new TextObject("{=BKHighTier}High Clan Tier"));

            return explained;
        }

        // Impacto de relação
        public virtual int GetRelationImpact(FeudalTitle title)
        {
            return title.TitleType switch
            {
                TitleType.Empire => -100,
                TitleType.Kingdom => -80,
                TitleType.Dukedom => -60,
                TitleType.County => -40,
                TitleType.Barony => -20,
                TitleType.Lordship => -10,
                _ => -30
            };
        }

        // Recompensa de skill
        public virtual int GetSkillReward(TitleType titleType, ActionType actionType)
        {
            var baseReward = titleType switch
            {
                TitleType.Empire => 500,
                TitleType.Kingdom => 300,
                TitleType.Dukedom => 200,
                TitleType.County => 100,
                TitleType.Barony => 50,
                TitleType.Lordship => 25,
                _ => 50
            };

            var multiplier = actionType switch
            {
                ActionType.Create => 1.5f,
                ActionType.Usurp => 1.2f,
                ActionType.Grant => 1.0f,
                ActionType.Claim => 0.5f,
                _ => 1.0f
            };

            return (int)(baseReward * multiplier);
        }

        // Score de herdeiro para herança
        public virtual ExplainedNumber GetInheritanceHeirScore(Hero currentLeader, Hero candidate, FeudalContract contract, bool explanations = false)
        {
            var explained = new ExplainedNumber(0f, explanations, new TextObject("{=BKInheritanceScore}Inheritance Score"));

            if (candidate == null || currentLeader == null)
                return explained;

            // Base: relação com líder atual
            explained.Add(currentLeader.GetRelation(candidate), new TextObject("{=BKRelation}Relation"));

            // Idade (mais velho = melhor para primogenitura)
            if (contract?.Inheritance?.StringId == DefaultInheritances.Instance.Primogeniture.StringId)
            {
                explained.Add((float)(candidate.Age - 18) * 0.5f, new TextObject("{=BKAge}Age"));
            }
            else if (contract?.Inheritance?.StringId == DefaultInheritances.Instance.Ultimogeniture.StringId)
            {
                explained.Add((float)(50 - candidate.Age) * 0.5f, new TextObject("{=BKYouth}Youth"));
            }

            // Skill de Lordship
            explained.Add(candidate.GetSkillValue(DefaultSkills.Leadership) * 0.5f, new TextObject("{=BKLordshipSkill}Lordship Skill"));

            // Traços
            if (candidate.GetTraitLevel(DefaultTraits.Honor) > 0)
                explained.Add(10f * candidate.GetTraitLevel(DefaultTraits.Honor), new TextObject("{=BKHonor}Honor"));

            if (candidate.GetTraitLevel(DefaultTraits.Calculating) > 0)
                explained.Add(5f * candidate.GetTraitLevel(DefaultTraits.Calculating), new TextObject("{=BKCalculating}Calculating"));

            return explained;
        }

        // Score de sucessão
        public virtual ExplainedNumber GetSuccessionHeirScore(Hero currentLeader, Hero candidate, FeudalTitle title, bool explanations = false)
        {
            var explained = new ExplainedNumber(0f, explanations, new TextObject("{=BKSuccessionScore}Succession Score"));

            if (candidate == null || currentLeader == null || title == null)
                return explained;

            var succession = title.Contract?.Succession ?? DefaultSuccessions.Instance.Hereditary;

            if (succession.IsElective)
            {
                // Sucessão eletiva: influência, relação com vassalos
                explained.Add(candidate.Clan.Influence * 0.01f, new TextObject("{=BKInfluence}Clan Influence"));

                // Relação com outros vassalos
                var vassals = TitleManager.Instance.CalculateVassals(candidate.Clan);
                float relationSum = 0;
                int count = 0;
                foreach (var kvp in vassals)
                {
                    relationSum += candidate.GetRelation(kvp.Key.Leader);
                    count++;
                }
                if (count > 0)
                    explained.Add(relationSum / count * 0.5f, new TextObject("{=BKVassalRelations}Vassal Relations"));
            }
            else
            {
                // Sucessão hereditária: usar herança
                var inheritance = title.Contract?.Inheritance ?? DefaultInheritances.Instance.Primogeniture;
                var contract = new FeudalContract(
                    title.Contract.Government,
                    title.Contract.Succession,
                    inheritance,
                    title.Contract.GenderLaw);
                return GetInheritanceHeirScore(currentLeader, candidate, contract, explanations);
            }

            return explained;
        }

        // Candidatos para concessão
        public virtual List<Hero> GetGrantCandidates(Hero grantor)
        {
            var heroes = new List<Hero>();
            var kingdom = grantor.Clan.Kingdom;
            if (kingdom != null)
                foreach (var clan in kingdom.Clans)
                    if (!clan.IsUnderMercenaryService && clan != grantor.Clan)
                        heroes.Add(clan.Leader);

            return heroes;
        }
    }
}