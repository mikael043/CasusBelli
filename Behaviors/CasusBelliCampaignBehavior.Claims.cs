using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public class FabricationProject
    {
        public string ClaimantKingdomId { get; set; }
        public string SettlementId { get; set; }
        public double StartedOnDay { get; set; }
        public double EndsOnDay { get; set; }

        public FabricationProject()
        {
            ClaimantKingdomId = "";
            SettlementId = "";
            StartedOnDay = 0.0;
            EndsOnDay = 0.0;
        }
    }

    // Segunda metade do behavior. Fica em arquivo separado para o arquivo
    // principal nao crescer sem controle. O acesso aos membros privados
    // do arquivo principal funciona normalmente (classe partial).
    public partial class CasusBelliCampaignBehavior
    {
     
        public static readonly double FabricationDays = 30.0;
        public static readonly float ClaimWarInfluenceCost = 20f;

        private static readonly int FabricationBaseGold = 10000;
        private static readonly float FabricationBaseInfluence = 60f;
        private static readonly int FabricatedClaimStrength = 50;
        private static readonly double FabricatedClaimDurationDays = 180.0;

        // 0 desliga. Aplicada quando uma guerra comeca SEM casus belli.
        private static readonly float UnassignedWarInfluencePenalty = 40f;
        private static readonly bool PenalizeAiUnassignedWars = false;

        private static readonly double TruceDays = 60.0;
        private static readonly bool AutoPeaceAfterVictory = true;
        private static readonly double AutoPeaceAfterVictoryDays = 10.0;

        private static readonly bool AiEnabled = true;
        private static readonly float AiFabricationWeeklyChance = 0.10f;
        private static readonly float AiClaimWarDailyChance = 0.02f;
        private static readonly int AiMaxSimultaneousWars = 10;

        private const int FieldsPerWarV2 = 11;
        private const int FieldsPerFabrication = 4;

        private List<FabricationProject> _fabricationProjects =
            new List<FabricationProject>();
        private List<string> _savedFabricationData = new List<string>();
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

        private void SaveFabricationProjects()
        {
            if (_savedFabricationData == null)
            {
                _savedFabricationData = new List<string>();
            }

            _savedFabricationData.Clear();

            foreach (FabricationProject project in _fabricationProjects)
            {
                _savedFabricationData.Add(project.ClaimantKingdomId ?? "");
                _savedFabricationData.Add(project.SettlementId ?? "");
                _savedFabricationData.Add(FormatDouble(project.StartedOnDay));
                _savedFabricationData.Add(FormatDouble(project.EndsOnDay));
            }
        }

        private void LoadFabricationProjects()
        {
            _fabricationProjects.Clear();

            if (_savedFabricationData == null)
            {
                _savedFabricationData = new List<string>();
            }

            for (int i = 0;
                 i + FieldsPerFabrication <= _savedFabricationData.Count;
                 i += FieldsPerFabrication)
            {
                FabricationProject project = new FabricationProject
                {
                    ClaimantKingdomId = _savedFabricationData[i],
                    SettlementId = _savedFabricationData[i + 1],
                    StartedOnDay = ParseDouble(_savedFabricationData[i + 2], 0.0),
                    EndsOnDay = ParseDouble(_savedFabricationData[i + 3], 0.0)
                };

                if (string.IsNullOrWhiteSpace(project.ClaimantKingdomId) ||
                    string.IsNullOrWhiteSpace(project.SettlementId))
                {
                    continue;
                }

                _fabricationProjects.Add(project);
            }
        }

        // ------------------------------------------------------------
        // AJUDANTES
        // ------------------------------------------------------------
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

        private string KingdomLabel(string kingdomId)
        {
            if (string.IsNullOrEmpty(kingdomId))
            {
                return "(nenhum)";
            }

            Kingdom kingdom = GetKingdom(kingdomId);

            return kingdom != null ? kingdom.Name.ToString() : kingdomId;
        }

        // Se nao compilar, troque por settlement.GetPosition2D ou
        // settlement.Position2D, conforme a sua versao do jogo.
        private static Vec2 GetSettlementPosition(Settlement settlement)
        {
            return settlement.GetPosition2D;
        }

        private float DistanceToNearestOwnedFortification(
            Kingdom kingdom,
            Settlement target)
        {
            float best = 300f;
            bool found = false;

            foreach (Settlement own in kingdom.Settlements)
            {
                if (own == null || !own.IsFortification)
                {
                    continue;
                }

                float distance = GetSettlementPosition(own).Distance(
                    GetSettlementPosition(target));

                if (!found || distance < best)
                {
                    best = distance;
                    found = true;
                }
            }

            return best;
        }

        // ------------------------------------------------------------
        // TREGUA
        // ------------------------------------------------------------
        public bool IsTruceActive(string kingdomId1, string kingdomId2)
        {
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
            out ClaimRecord claim,
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

            if (!_claimRegistry.TryGetUsableClaim(
                attacker.StringId,
                settlement.StringId,
                CampaignTime.Now.ToDays,
                _settlementDeJureRegistry,
                out claim))
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
            ClaimRecord claim;

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
                ClaimId = claim.ClaimId
            };

            DeclareWarAction.ApplyByDefault(attacker, defender);

            _pendingClaimWar = null;

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
                claim.ClaimId + " | Alvo: " + settlement.StringId;
            return true;
        }

        // Fecha a ficha de uma guerra que acabou (paz).
        private void FinalizeWar(WarRecord war)
        {
            war.EndedOnDay = CampaignTime.Now.ToDays;

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

        // Paz automatica alguns dias depois de o objetivo ser conquistado.
        private void ProcessClaimWarAutoPeace()
        {
            if (!AutoPeaceAfterVictory)
            {
                return;
            }

            double now = CampaignTime.Now.ToDays;
            List<WarRecord> toEnd = new List<WarRecord>();

            foreach (WarRecord war in _warRegistry.AllWars)
            {
                if (war.IsActive &&
                    war.CasusBelliType == ClaimWarCasusBelliType &&
                    war.ObjectiveAchievedOnDay >= 0.0 &&
                    now - war.ObjectiveAchievedOnDay >=
                        AutoPeaceAfterVictoryDays)
                {
                    toEnd.Add(war);
                }
            }

            foreach (WarRecord war in toEnd)
            {
                Kingdom attacker = GetKingdom(war.AttackerKingdomId);
                Kingdom defender = GetKingdom(war.DefenderKingdomId);

                if (attacker == null ||
                    defender == null ||
                    !attacker.IsAtWarWith(defender))
                {
                    continue;
                }

                MakePeaceAction.Apply(attacker, defender);
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

            if (!isPlayer && !PenalizeAiUnassignedWars)
            {
                return;
            }

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
        public bool TryFindBestClaimAgainst(
            Kingdom attacker,
            Kingdom defender,
            out ClaimRecord bestClaim,
            out Settlement bestSettlement)
        {
            bestClaim = null;
            bestSettlement = null;

            if (attacker == null || defender == null)
            {
                return false;
            }

            List<ClaimRecord> claims =
                _claimRegistry.GetUsableClaimsForKingdom(
                    attacker.StringId,
                    CampaignTime.Now.ToDays,
                    _settlementDeJureRegistry);

            foreach (ClaimRecord claim in claims)
            {
                Settlement settlement = Settlement.Find(claim.SettlementId);

                if (settlement == null ||
                    !settlement.IsFortification ||
                    GetKingdomId(settlement.Owner) != defender.StringId)
                {
                    continue;
                }

                if (bestClaim == null || claim.Strength > bestClaim.Strength)
                {
                    bestClaim = claim;
                    bestSettlement = settlement;
                }
            }

            return bestClaim != null;
        }

        public bool HasUsableClaimAgainst(Kingdom attacker, Kingdom defender)
        {
            ClaimRecord claim;
            Settlement settlement;

            return TryFindBestClaimAgainst(
                attacker,
                defender,
                out claim,
                out settlement);
        }

        public bool CanUsurp(Kingdom kingdom, Settlement settlement)
        {
            return kingdom != null &&
                settlement != null &&
                IsActiveUsurpationAvailable(settlement) &&
                GetKingdomId(settlement.Owner) == kingdom.StringId;
        }

        // ------------------------------------------------------------
        // FABRICACAO DE CLAIMS
        // ------------------------------------------------------------
        public bool HasFabricationProject(string kingdomId)
        {
            foreach (FabricationProject project in _fabricationProjects)
            {
                if (project.ClaimantKingdomId == kingdomId)
                {
                    return true;
                }
            }

            return false;
        }

        public void GetFabricationCost(
            Kingdom attacker,
            Settlement settlement,
            out int gold,
            out float influence)
        {
            float typeFactor = settlement.IsTown ? 1.0f : 0.6f;

            float prosperity = settlement.Town != null
                ? settlement.Town.Prosperity
                : 1500f;
            float wealthFactor = 1f + prosperity / 3000f;

            float distance = DistanceToNearestOwnedFortification(
                attacker,
                settlement);
            float distanceFactor = 1f + Math.Min(distance, 400f) / 400f;

            // Mais barato fabricar contra quem ocupa um feudo que nao e dele.
            float usurpedFactor = 1f;
            SettlementDeJureRecord record;

            if (_settlementDeJureRegistry.TryGetRecord(
                    settlement.StringId,
                    out record) &&
                record.DeFactoKingdomId != record.DeJureKingdomId)
            {
                usurpedFactor = 0.7f;
            }

            float multiplier =
                typeFactor * wealthFactor * distanceFactor * usurpedFactor;

            gold = (int)(FabricationBaseGold * multiplier);
            influence = FabricationBaseInfluence * multiplier;
        }

        public bool CanStartFabrication(
            Kingdom attacker,
            Settlement settlement,
            bool ignoreCosts,
            out string message)
        {
            message = "";

            if (attacker == null)
            {
                message = "reino nao encontrado";
                return false;
            }

            if (settlement == null || !settlement.IsFortification)
            {
                message = "feudo invalido (so cidades e castelos)";
                return false;
            }

            Kingdom owner = GetSettlementOwnerKingdom(settlement);

            if (owner == null || owner == attacker)
            {
                message = "o feudo nao pertence a outro reino";
                return false;
            }

            if (HasFabricationProject(attacker.StringId))
            {
                message = "ja ha uma fabricacao em andamento";
                return false;
            }

            ClaimRecord existing;

            if (_claimRegistry.TryGetUsableClaim(
                attacker.StringId,
                settlement.StringId,
                CampaignTime.Now.ToDays,
                _settlementDeJureRegistry,
                out existing))
            {
                message = "o reino ja tem claim sobre este feudo";
                return false;
            }

            if (!ignoreCosts)
            {
                Clan ruler = attacker.RulingClan;

                if (ruler == null || ruler.Leader == null)
                {
                    message = "reino sem governante";
                    return false;
                }

                int gold;
                float influence;
                GetFabricationCost(attacker, settlement, out gold, out influence);

                if (ruler.Leader.Gold < gold)
                {
                    message = "faltam " + (gold - ruler.Leader.Gold) +
                        " denarios (custo " + gold + ")";
                    return false;
                }

                if (ruler.Influence < influence)
                {
                    message = "faltam " +
                        (influence - ruler.Influence).ToString("0") +
                        " de influencia (custo " +
                        influence.ToString("0") + ")";
                    return false;
                }
            }

            return true;
        }

        public bool TryStartFabrication(
            string kingdomId,
            string settlementId,
            bool ignoreCosts,
            out string message)
        {
            Kingdom attacker = GetKingdom(kingdomId);
            Settlement settlement = string.IsNullOrEmpty(settlementId)
                ? null
                : Settlement.Find(settlementId);

            if (!CanStartFabrication(
                attacker,
                settlement,
                ignoreCosts,
                out message))
            {
                return false;
            }

            int gold;
            float influence;
            GetFabricationCost(attacker, settlement, out gold, out influence);

            if (!ignoreCosts)
            {
                Clan ruler = attacker.RulingClan;

                GiveGoldAction.ApplyBetweenCharacters(
                    ruler.Leader,
                    null,
                    gold,
                    true);

                ChangeClanInfluenceAction.Apply(ruler, -influence);
            }

            double now = CampaignTime.Now.ToDays;

            _fabricationProjects.Add(new FabricationProject
            {
                ClaimantKingdomId = attacker.StringId,
                SettlementId = settlement.StringId,
                StartedOnDay = now,
                EndsOnDay = now + FabricationDays
            });

            message = "fabricação iniciada: " + settlement.StringId +
                " | termina em " + FabricationDays.ToString("0") +
                " dias" + (ignoreCosts
                    ? " (sem custo)"
                    : " | custo: " + gold + " denarios e " +
                      influence.ToString("0") + " de influencia");
            return true;
        }

        private void ProcessFabricationProjects()
        {
            if (_fabricationProjects.Count == 0)
            {
                return;
            }

            double now = CampaignTime.Now.ToDays;
            List<FabricationProject> finished = new List<FabricationProject>();
            List<FabricationProject> cancelled = new List<FabricationProject>();

            foreach (FabricationProject project in _fabricationProjects)
            {
                Kingdom kingdom = GetKingdom(project.ClaimantKingdomId);
                Settlement settlement = Settlement.Find(project.SettlementId);

                if (kingdom == null ||
                    kingdom.IsEliminated ||
                    settlement == null ||
                    GetKingdomId(settlement.Owner) == kingdom.StringId)
                {
                    cancelled.Add(project);
                    continue;
                }

                if (now >= project.EndsOnDay)
                {
                    finished.Add(project);
                }
            }

            foreach (FabricationProject project in cancelled)
            {
                _fabricationProjects.Remove(project);
            }

            foreach (FabricationProject project in finished)
            {
                _fabricationProjects.Remove(project);

                ClaimRecord claim = new ClaimRecord
                {
                    SettlementId = project.SettlementId,
                    ClaimantKingdomId = project.ClaimantKingdomId,
                    Source = ClaimSource.Fabricated,
                    Strength = FabricatedClaimStrength,
                    CreatedOnDay = now,
                    ExpiresOnDay = now + FabricatedClaimDurationDays
                };

                if (_claimRegistry.AddRecord(claim, now) &&
                    IsPlayerKingdom(GetKingdom(project.ClaimantKingdomId)))
                {
                    ShowMessage("CB: reivindicação fabricada sobre " +
                        project.SettlementId + ".");
                }
            }
        }

        // ------------------------------------------------------------
        // IA BASICA (mesmas funcoes e mesmos custos do jogador)
        // ------------------------------------------------------------
        private void RunDailyAi()
        {
            if (!AiEnabled ||
                !_diplomacyInitialized ||
                !_deJureOwnershipInitialized)
            {
                return;
            }

            double now = CampaignTime.Now.ToDays;
            bool isWeekStart = _aiForce || ((int)now) % 7 == 0;
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

                if (isWeekStart)
                {
                    TryAiStartFabrication(kingdom);
                }

                TryAiDeclareClaimWar(kingdom, now);
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

        private void TryAiStartFabrication(Kingdom kingdom)
        {
            if (HasFabricationProject(kingdom.StringId))
            {
                return;
            }

            float chance = _aiForce ? 1.1f : AiFabricationWeeklyChance;

            if (MBRandom.RandomFloat > chance)
            {
                return;
            }

            Settlement best = null;
            float bestScore = 0f;

            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null || !settlement.IsFortification)
                {
                    continue;
                }

                string reason;

                if (!CanStartFabrication(kingdom, settlement, false, out reason))
                {
                    continue;
                }

                float score = ScoreFabricationTarget(kingdom, settlement);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = settlement;
                }
            }

            if (best == null)
            {
                return;
            }

            string message;
            TryStartFabrication(kingdom.StringId, best.StringId, false, out message);
        }

        private float ScoreFabricationTarget(
            Kingdom kingdom,
            Settlement settlement)
        {
            Kingdom owner = GetSettlementOwnerKingdom(settlement);

            float value = settlement.Town != null
                ? Math.Max(0.3f, settlement.Town.Prosperity / 1000f)
                : 1f;

            float distance = DistanceToNearestOwnedFortification(
                kingdom,
                settlement);
            float proximity = 1f / (1f + distance / 100f);

            float atWar = owner != null && kingdom.IsAtWarWith(owner)
                ? 1.5f
                : 1f;

            float usurped = 1f;
            SettlementDeJureRecord record;

            if (_settlementDeJureRegistry.TryGetRecord(
                    settlement.StringId,
                    out record) &&
                record.DeFactoKingdomId != record.DeJureKingdomId)
            {
                usurped = 1.3f;
            }

            float noise = 0.5f + MBRandom.RandomFloat;

            return value * proximity * atWar * usurped * noise;
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

        private void TryAiDeclareClaimWar(Kingdom kingdom, double now)
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

            List<ClaimRecord> claims =
                _claimRegistry.GetUsableClaimsForKingdom(
                    kingdom.StringId,
                    now,
                    _settlementDeJureRegistry);

            ClaimRecord best = null;

            foreach (ClaimRecord claim in claims)
            {
                Settlement settlement = Settlement.Find(claim.SettlementId);

                if (settlement == null || !settlement.IsFortification)
                {
                    continue;
                }

                Kingdom defender = GetSettlementOwnerKingdom(settlement);

                if (defender == null ||
                    defender == kingdom ||
                    kingdom.IsAtWarWith(defender) ||
                    IsTruceActive(kingdom.StringId, defender.StringId))
                {
                    continue;
                }

                if (best == null || claim.Strength > best.Strength)
                {
                    best = claim;
                }
            }

            if (best == null)
            {
                return;
            }

            string message;
            TryDeclareClaimWar(kingdom.StringId, best.SettlementId, false, out message);
        }

        // ------------------------------------------------------------
        // TEXTOS PARA MENU E COMANDOS DE DEBUG
        // ------------------------------------------------------------
        public string GetFabricationProjectsText()
        {
            if (_fabricationProjects.Count == 0)
            {
                return "CB: nenhuma fabricacao em andamento.";
            }

            StringBuilder result = new StringBuilder();
            double now = CampaignTime.Now.ToDays;

            result.Append("CB: fabricacoes em andamento: " +
                _fabricationProjects.Count + "\n");

            foreach (FabricationProject project in _fabricationProjects)
            {
                result.Append(project.ClaimantKingdomId);
                result.Append(" -> ");
                result.Append(project.SettlementId);
                result.Append(" | faltam ");
                result.Append(Math.Max(0.0, project.EndsOnDay - now).ToString("0.0"));
                result.Append(" dias\n");
            }

            return result.ToString();
        }

        public string BuildSettlementStatus(Settlement settlement, Kingdom viewer)
        {
            StringBuilder text = new StringBuilder();
            double now = CampaignTime.Now.ToDays;

            string ownerId = GetKingdomId(settlement.Owner);

            text.Append(settlement.Name + " (" + settlement.StringId + ")\n");
            text.Append("Dono atual: " + KingdomLabel(ownerId) + "\n");

            SettlementDeJureRecord record;

            if (_settlementDeJureRegistry.TryGetRecord(
                settlement.StringId,
                out record))
            {
                text.Append("De jure: " + KingdomLabel(record.DeJureKingdomId) +
                    " | De facto: " + KingdomLabel(record.DeFactoKingdomId) + "\n");

                if (record.DeFactoKingdomId != record.DeJureKingdomId)
                {
                    text.Append("Ocupado ha " +
                        GetOccupiedDays(record).ToString("0") +
                        " dias (usurpacao passiva aos " +
                        PassiveUsurpationDays.ToString("0") + ")\n");
                }

                text.Append("Usurpacao ativa: " +
                    (IsActiveUsurpationAvailable(settlement)
                        ? "disponivel"
                        : "bloqueada") + "\n");
            }

            if (viewer == null)
            {
                return text.ToString();
            }

            ClaimRecord claim;

            if (_claimRegistry.TryGetUsableClaim(
                viewer.StringId,
                settlement.StringId,
                now,
                _settlementDeJureRegistry,
                out claim))
            {
                text.Append("Claim do seu reino: " + claim.Source +
                    " (forca " + claim.Strength + ", " +
                    (claim.ExpiresOnDay <= 0.0
                        ? "permanente"
                        : "expira em " +
                          (claim.ExpiresOnDay - now).ToString("0") + " dias") +
                    ")\n");
            }
            else
            {
                text.Append("Claim do seu reino: nenhum\n");
            }

            foreach (FabricationProject project in _fabricationProjects)
            {
                if (project.ClaimantKingdomId == viewer.StringId &&
                    project.SettlementId == settlement.StringId)
                {
                    text.Append("Fabricacao em andamento: faltam " +
                        Math.Max(0.0, project.EndsOnDay - now).ToString("0") +
                        " dias\n");
                }
            }

            if (!string.IsNullOrEmpty(ownerId) && ownerId != viewer.StringId)
            {
                if (IsTruceActive(viewer.StringId, ownerId))
                {
                    text.Append("Tregua com o dono: " +
                        GetTruceDaysLeft(viewer.StringId, ownerId).ToString("0") +
                        " dias restantes\n");
                }

                Kingdom ownerKingdom = GetKingdom(ownerId);

                if (ownerKingdom != null && settlement.IsFortification)
                {
                    int gold;
                    float influence;
                    GetFabricationCost(viewer, settlement, out gold, out influence);

                    text.Append("Fabricar claim: " + gold + " denarios + " +
                        influence.ToString("0") + " influencia, " +
                        FabricationDays.ToString("0") + " dias\n");
                    text.Append("Guerra de reivindicacao: " +
                        ClaimWarInfluenceCost.ToString("0") + " influencia\n");
                }
            }

            return text.ToString();
        }
    }
}
