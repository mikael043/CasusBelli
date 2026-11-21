using CasusBelli.Managers.Titles;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public partial class CasusBelliCampaignBehavior
    {
     
        public static readonly float ClaimWarInfluenceCost = 20f;

        // 0 desliga. Aplicada quando uma guerra comeca SEM casus belli.
        private static readonly float UnassignedWarInfluencePenalty = 40f;

        private static readonly double TruceDays = 60.0;

        private static readonly bool AiEnabled = true;
        private static readonly float AiFabricationWeeklyChance = 0.10f;
        private static readonly float AiClaimWarDailyChance = 0.02f;
        private static readonly int AiMaxSimultaneousWars = 10;

        private const int FieldsPerWarV2 = 11;
        private List<string> _savedWarDataV2 = new List<string>();
        private bool _aiForce = false;


        // ------------------------------------------------------------
        // PERSISTENCIA
        // ------------------------------------------------------------
        private static string FormatDouble(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string text, double fallback)
        {
            double value;

            if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                return value;
            }

            return fallback;
        }

        private void SaveWarRecordsToData()
        {
            if (_savedWarDataV2 == null)
            {
                _savedWarDataV2 = new List<string>();
            }

            _savedWarDataV2.Clear();

            foreach (WarRecord war in _warRegistry.AllWars)
            {
                _savedWarDataV2.Add(war.WarId ?? "");
                _savedWarDataV2.Add(war.AttackerKingdomId ?? "");
                _savedWarDataV2.Add(war.DefenderKingdomId ?? "");
                _savedWarDataV2.Add(war.CasusBelliType ?? "");
                _savedWarDataV2.Add(war.TargetSettlementId ?? "");
                _savedWarDataV2.Add(war.IsActive.ToString());
                _savedWarDataV2.Add(FormatDouble(war.StartedOnDay));
                _savedWarDataV2.Add(FormatDouble(war.EndedOnDay));
                _savedWarDataV2.Add(FormatDouble(war.ObjectiveAchievedOnDay));
                _savedWarDataV2.Add(war.Outcome ?? "");
                _savedWarDataV2.Add(war.ClaimId ?? "");
            }
        }

        // Le o formato v2; se nao houver, migra o v1 (6 campos por guerra).
        private void LoadWarRecordsFromData()
        {
            if (_savedWarDataV2 == null)
            {
                _savedWarDataV2 = new List<string>();
            }

            if (_savedWarDataV2.Count >= FieldsPerWarV2)
            {
                for (int i = 0;
                     i + FieldsPerWarV2 <= _savedWarDataV2.Count;
                     i += FieldsPerWarV2)
                {
                    bool isActive;

                    if (!bool.TryParse(_savedWarDataV2[i + 5], out isActive))
                    {
                        continue;
                    }

                    WarRecord war = new WarRecord
                    {
                        WarId = _savedWarDataV2[i],
                        AttackerKingdomId = _savedWarDataV2[i + 1],
                        DefenderKingdomId = _savedWarDataV2[i + 2],
                        CasusBelliType = _savedWarDataV2[i + 3],
                        TargetSettlementId = _savedWarDataV2[i + 4],
                        IsActive = isActive,
                        StartedOnDay = ParseDouble(_savedWarDataV2[i + 6], -1.0),
                        EndedOnDay = ParseDouble(_savedWarDataV2[i + 7], -1.0),
                        ObjectiveAchievedOnDay =
                            ParseDouble(_savedWarDataV2[i + 8], -1.0),
                        Outcome = _savedWarDataV2[i + 9],
                        ClaimId = _savedWarDataV2[i + 10]
                    };

                    _warRegistry.AddWar(war);
                }

                return;
            }

            if (_savedWarData == null)
            {
                _savedWarData = new List<string>();
            }

            for (int i = 0;
                 i + FieldsPerWar <= _savedWarData.Count;
                 i += FieldsPerWar)
            {
                bool isActive;

                if (!bool.TryParse(_savedWarData[i + 5], out isActive))
                {
                    continue;
                }

                WarRecord war = new WarRecord
                {
                    WarId = _savedWarData[i],
                    AttackerKingdomId = _savedWarData[i + 1],
                    DefenderKingdomId = _savedWarData[i + 2],
                    CasusBelliType = _savedWarData[i + 3],
                    TargetSettlementId = _savedWarData[i + 4],
                    IsActive = isActive
                };

                _warRegistry.AddWar(war);
            }
        }

        private static bool IsPlayerKingdom(Kingdom kingdom)
        {
            return kingdom != null &&
                Clan.PlayerClan != null &&
                Clan.PlayerClan.Kingdom == kingdom;
        }

        private static void ShowMessage(string text)
        {
            InformationManager.DisplayMessage(new InformationMessage(text));
        }

        public Kingdom GetSettlementOwnerKingdom(Settlement settlement)
        {
            if (settlement == null)
            {
                return null;
            }

            return GetKingdom(GetKingdomId(settlement.Owner));
        }

        public bool IsTruceActive(string kingdomId1, string kingdomId2)
        {
            if (TryGetActiveWarBetween(kingdomId1, kingdomId2, out var ongoing) && ongoing.ArmisticeEnds >= 0)
                return true;
            double lastEnded = -1.0;

            foreach (WarRecord war in _warRegistry.AllWars)
            {
                if (war.IsActive || war.EndedOnDay < 0.0)
                {
                    continue;
                }

                bool samePair =
                    (war.AttackerKingdomId == kingdomId1 &&
                     war.DefenderKingdomId == kingdomId2) ||
                    (war.AttackerKingdomId == kingdomId2 &&
                     war.DefenderKingdomId == kingdomId1);

                if (samePair && war.EndedOnDay > lastEnded)
                {
                    lastEnded = war.EndedOnDay;
                }
            }

            return lastEnded >= 0.0 &&
                CampaignTime.Now.ToDays < lastEnded + TruceDays;
        }

        private double GetTruceDaysLeft(string kingdomId1, string kingdomId2)
        {
            if (TryGetActiveWarBetween(kingdomId1, kingdomId2, out var ongoing) && ongoing.ArmisticeEnds >= 0)
                return Math.Max(0, ongoing.ArmisticeEnds - CampaignTime.Now.ToDays);
            double lastEnded = -1.0;

            foreach (WarRecord war in _warRegistry.AllWars)
            {
                if (war.IsActive || war.EndedOnDay < 0.0)
                {
                    continue;
                }

                bool samePair =
                    (war.AttackerKingdomId == kingdomId1 &&
                     war.DefenderKingdomId == kingdomId2) ||
                    (war.AttackerKingdomId == kingdomId2 &&
                     war.DefenderKingdomId == kingdomId1);

                if (samePair && war.EndedOnDay > lastEnded)
                {
                    lastEnded = war.EndedOnDay;
                }
            }

            if (lastEnded < 0.0)
            {
                return 0.0;
            }

            return Math.Max(
                0.0,
                lastEnded + TruceDays - CampaignTime.Now.ToDays);
        }

        // ------------------------------------------------------------
        // GUERRA DE REIVINDICACAO
        // ------------------------------------------------------------
        public bool CheckClaimWar(
            Kingdom attacker,
            Settlement settlement,
            bool ignoreCosts,
            out Kingdom defender,
            out FeudalTitle claim,
            out string message)
        {
            defender = null;
            claim = null;
            message = "";

            if (attacker == null)
            {
                message = "reino atacante nao encontrado";
                return false;
            }

            if (settlement == null || !settlement.IsFortification)
            {
                message = "feudo invalido (so cidades e castelos)";
                return false;
            }

            defender = GetSettlementOwnerKingdom(settlement);

            if (defender == null || defender == attacker)
            {
                message = "o feudo nao pertence a outro reino";
                return false;
            }

            string pairKey = GetPairKey(attacker.StringId, defender.StringId);

            if (pairKey == null ||
                !_eligiblePairs.Contains(pairKey) ||
                attacker.IsAtWarWith(defender))
            {
                message = "os reinos ja estao em guerra";
                return false;
            }

            claim = TitleManager.Instance.GetTitle(settlement);
            if (!HasTitleRight(attacker, claim))
            {
                message = "o reino nao tem claim sobre este feudo";
                return false;
            }

            if (!ignoreCosts)
            {
                if (IsTruceActive(attacker.StringId, defender.StringId))
                {
                    message = "ha uma tregua em vigor (" +
                        GetTruceDaysLeft(
                            attacker.StringId,
                            defender.StringId).ToString("0") +
                        " dias)";
                    return false;
                }

                Clan ruler = attacker.RulingClan;

                if (ruler == null || ruler.Influence < ClaimWarInfluenceCost)
                {
                    message = "influencia insuficiente (custo " +
                        ClaimWarInfluenceCost.ToString("0") + ")";
                    return false;
                }
            }

            return true;
        }

        // Declara uma guerra de reivindicacao. A ficha de guerra e criada
        // pelo OnWarDeclared com tipo "CLAIM", o feudo-alvo e o claim usado.
        public bool TryDeclareClaimWar(
            string attackerKingdomId,
            string settlementId,
            bool ignoreCosts,
            out string message)
        {
            Kingdom attacker = GetKingdom(attackerKingdomId);
            Settlement settlement = string.IsNullOrEmpty(settlementId)
                ? null
                : Settlement.Find(settlementId);
            Kingdom defender;
            FeudalTitle claim;

            if (!CheckClaimWar(
                attacker,
                settlement,
                ignoreCosts,
                out defender,
                out claim,
                out message))
            {
                return false;
            }

            _pendingClaimWar = new PendingClaimWar
            {
                AttackerKingdomId = attacker.StringId,
                DefenderKingdomId = defender.StringId,
                SettlementId = settlement.StringId,
                ClaimId = claim.StringId
            };

            try
            {
                DeclareWarAction.ApplyByDefault(attacker, defender);
            }
            finally
            {
                _pendingClaimWar = null;
            }

            if (!attacker.IsAtWarWith(defender))
            {
                message = "o jogo nao declarou a guerra";
                return false;
            }

            if (!ignoreCosts && attacker.RulingClan != null)
            {
                ChangeClanInfluenceAction.Apply(
                    attacker.RulingClan,
                    -ClaimWarInfluenceCost);
            }

            message = "guerra de reivindicacao declarada | Claim: " +
                claim.StringId + " | Alvo: " + settlement.StringId;
            return true;
        }

        // Fecha a ficha de uma guerra que acabou (paz).
        private void FinalizeWar(WarRecord war)
        {
            war.EndedOnDay = CampaignTime.Now.ToDays;
            if (!string.IsNullOrEmpty(war.Outcome)) return;

            if (war.CasusBelliType == ClaimWarCasusBelliType)
            {
                war.Outcome = war.ObjectiveAchievedOnDay >= 0.0
                    ? "VICTORY"
                    : "WHITE_PEACE";
            }
            else
            {
                war.Outcome = "PEACE";
            }
        }

        // O feudo-alvo de uma guerra de claim foi conquistado pelo atacante.
        private void MarkClaimWarObjective(
            Settlement settlement,
            Hero newOwner)
        {
            string newKingdomId = GetKingdomId(newOwner);

            if (string.IsNullOrEmpty(newKingdomId))
            {
                return;
            }

            foreach (WarRecord war in _warRegistry.AllWars)
            {
                if (!war.IsActive ||
                    war.CasusBelliType != ClaimWarCasusBelliType ||
                    war.TargetSettlementId != settlement.StringId ||
                    war.AttackerKingdomId != newKingdomId ||
                    war.ObjectiveAchievedOnDay >= 0.0)
                {
                    continue;
                }

                war.ObjectiveAchievedOnDay = CampaignTime.Now.ToDays;

                if (IsPlayerKingdom(GetKingdom(newKingdomId)))
                {
                    ShowMessage("CB: objetivo da guerra de reivindicacao " +
                        "conquistado: " + settlement.Name);
                }
            }
        }

        // Guerra comecada sem nenhum casus belli custa influencia.
        private void ApplyUnassignedWarPenalty(Kingdom attacker)
        {
            if (UnassignedWarInfluencePenalty <= 0f ||
                CampaignTime.Now.ToDays < 2.0 ||
                attacker == null)
            {
                return;
            }

            bool isPlayer = IsPlayerKingdom(attacker);


            Clan ruler = attacker.RulingClan;

            if (ruler == null)
            {
                return;
            }

            ChangeClanInfluenceAction.Apply(
                ruler,
                -UnassignedWarInfluencePenalty);

            if (isPlayer)
            {
                ShowMessage("CB: guerra sem casus belli: -" +
                    UnassignedWarInfluencePenalty.ToString("0") +
                    " de influencia.");
            }
        }

        // Melhor claim que o atacante pode usar contra um feudo do defensor.
        // Usado pelo CasusBelliDiplomacyModel (a IA do jogo base) e pelo
        // OnWarDeclared (guerras declaradas pelo caminho normal do jogo).
        private static bool HasTitleRight(Kingdom kingdom, FeudalTitle title)
        {
            return kingdom != null && title != null &&
                (title.deJure?.Clan?.Kingdom == kingdom ||
                 title.Claims.Keys.Any(hero => !hero.IsDead && hero.Clan?.Kingdom == kingdom && title.HeroHasValidClaim(hero)));
        }

        public bool TryFindBestClaimAgainst(Kingdom attacker, Kingdom defender, out FeudalTitle title, out Settlement settlement)
        {
            title = defender?.Settlements.Where(f => f.IsFortification)
                .Select(f => TitleManager.Instance.GetTitle(f))
                .Where(t => HasTitleRight(attacker, t))
                .OrderBy(t => t.TitleType).FirstOrDefault();
            settlement = title?.Fief;
            return settlement != null;
        }

        public bool HasUsableClaimAgainst(Kingdom attacker, Kingdom defender)
        {
            return TryFindBestClaimAgainst(attacker, defender, out _, out _);
        }

        private void RunDailyAi()
        {
            if (!AiEnabled ||
                !_diplomacyInitialized ||
                !TitleManager.Instance.IsInitialized)
            {
                return;
            }

            double now = CampaignTime.Now.ToDays;
            Kingdom playerKingdom = Clan.PlayerClan != null
                ? Clan.PlayerClan.Kingdom
                : null;

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null ||
                    kingdom == playerKingdom ||
                    kingdom.IsEliminated ||
                    kingdom.RulingClan == null)
                {
                    continue;
                }

                if ((_aiForce || (int)now % 7 == 0) && (_aiForce || MBRandom.RandomFloat < AiFabricationWeeklyChance))
                    TryAiClaimTitle(kingdom);
                TryAiDeclareClaimWar(kingdom);
            }
        }

        public void RunAiForDebug()
        {
            _aiForce = true;

            try
            {
                RunDailyAi();
            }
            finally
            {
                _aiForce = false;
            }
        }

        private static void TryAiClaimTitle(Kingdom kingdom)
        {
            var ruler = kingdom.Leader;
            var manager = TitleManager.Instance;
            if (ruler == null || manager.AllTitles.Any(t => t.OngoingClaims.ContainsKey(ruler))) return;
            var targets = manager.AllTitles.Where(t => t.Fief?.IsFortification == true &&
                t.Fief.OwnerClan?.Kingdom != null && t.Fief.OwnerClan.Kingdom != kingdom &&
                !HasTitleRight(kingdom, t)).ToList();
            if (targets.Count == 0) return;
            var action = manager.GetModel().GetClaimAction(targets[MBRandom.RandomInt(targets.Count)], ruler);
            if (action.Possible && action.IsWilling) action.TakeAction(null);
        }

        private int CountActiveWars(Kingdom kingdom)
        {
            int count = 0;

            foreach (Kingdom other in Kingdom.All)
            {
                if (other != null &&
                    other != kingdom &&
                    !other.IsEliminated &&
                    kingdom.IsAtWarWith(other))
                {
                    count++;
                }
            }

            return count;
        }

        private void TryAiDeclareClaimWar(Kingdom kingdom)
        {
            if (CountActiveWars(kingdom) >= AiMaxSimultaneousWars)
            {
                return;
            }

            float chance = _aiForce ? 1.1f : AiClaimWarDailyChance;

            if (MBRandom.RandomFloat > chance)
            {
                return;
            }

            foreach (var defender in Kingdom.All)
            {
                if (defender == kingdom || kingdom.IsAtWarWith(defender) || IsTruceActive(kingdom.StringId, defender.StringId)) continue;
                if (TryFindBestClaimAgainst(kingdom, defender, out _, out var fief) &&
                    TryDeclareClaimWar(kingdom.StringId, fief.StringId, false, out _)) return;
            }
        }

        public string BuildSettlementStatus(Settlement settlement, Kingdom viewer)
        {
            var title = TitleManager.Instance.GetTitle(settlement);
            if (title == null) return "Feudo sem título registrado.";
            string status = $"{title.FullName}\nTitular de jure: {title.deJure?.Name}\nDetentor de facto: {title.DeFactoHolder?.Name}";
            var hero = Hero.MainHero;
            if (hero != null && title.OngoingClaims.TryGetValue(hero, out var deadline))
                status += $"\nSua reivindicação amadurece em {deadline.RemainingDaysFromNow:0} dias.";
            else if (hero != null && title.HeroHasValidClaim(hero))
                status += "\nVocê possui uma reivindicação sobre este título.";
            return status;
        }
    }
}
