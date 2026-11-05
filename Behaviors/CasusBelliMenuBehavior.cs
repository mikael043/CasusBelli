using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli
{
    // Menu de jogo para usar o sistema sem comandos dev:
    // dentro de uma cidade ou castelo, opcao "Casus Belli".
    // Tambem contem a janela de negociacao do soberano capturado.
    // Esta classe esta isolada de proposito: se algo nao compilar aqui,
    // remova o AddBehavior no SubModule e apague este arquivo.
    public class CasusBelliMenuBehavior : CampaignBehaviorBase
    {
        private const string MenuId = "casus_belli_menu";
        public const string NegotiationMenuId = "casus_belli_negotiation";

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static CasusBelliCampaignBehavior GetMain()
        {
            if (Campaign.Current == null)
            {
                return null;
            }

            return Campaign.Current.GetCampaignBehavior<
                CasusBelliCampaignBehavior>();
        }

        private static CasusBelliKingBehavior GetKings()
        {
            if (Campaign.Current == null)
            {
                return null;
            }

            return Campaign.Current.GetCampaignBehavior<
                CasusBelliKingBehavior>();
        }

        private static Kingdom GetPlayerKingdom()
        {
            if (Hero.MainHero == null || Hero.MainHero.Clan == null)
            {
                return null;
            }

            return Hero.MainHero.Clan.Kingdom;
        }

        private static bool IsPlayerRuler(Kingdom kingdom)
        {
            return kingdom != null &&
                Hero.MainHero != null &&
                kingdom.RulingClan == Hero.MainHero.Clan;
        }

        private static string VanillaMenuId()
        {
            Settlement settlement = Settlement.CurrentSettlement;

            return settlement != null && settlement.IsCastle
                ? "castle"
                : "town";
        }

        private static void Show(string text)
        {
            InformationManager.DisplayMessage(new InformationMessage(text));
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddEntryOption(starter, "town");
            AddEntryOption(starter, "castle");
            AddPeaceEntryOption(starter, "town");
            AddPeaceEntryOption(starter, "castle");

            starter.AddGameMenu(
                MenuId,
                "{CB_STATUS}",
                args => OnMenuInit(args));

            // Fabricar claim
            starter.AddGameMenuOption(
                MenuId,
                "casus_belli_fabricate",
                "Fabricar reivindicacao",
                args => FabricateCondition(args),
                args => FabricateConsequence(args));

            // Declarar guerra de reivindicacao
            starter.AddGameMenuOption(
                MenuId,
                "casus_belli_declare",
                "Declarar guerra de reivindicacao",
                args => DeclareCondition(args),
                args => DeclareConsequence(args));

            // Usurpacao ativa
            starter.AddGameMenuOption(
                MenuId,
                "casus_belli_usurp",
                "Usurpar o direito de jure",
                args => UsurpCondition(args),
                args => UsurpConsequence(args));

            // Voltar
            starter.AddGameMenuOption(
                MenuId,
                "casus_belli_back",
                "Voltar",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                args => GameMenu.SwitchToMenu(VanillaMenuId()),
                true);

            AddPeaceMenu(starter);
            AddNegotiationMenu(starter);
        }

        private void AddEntryOption(CampaignGameStarter starter, string menuId)
        {
            starter.AddGameMenuOption(
                menuId,
                "casus_belli_entry",
                "Casus Belli: reivindicacoes",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

                    Settlement settlement = Settlement.CurrentSettlement;

                    return settlement != null &&
                        settlement.IsFortification &&
                        GetPlayerKingdom() != null &&
                        GetMain() != null;
                },
                args => GameMenu.SwitchToMenu(MenuId),
                false,
                1);
        }

        private void OnMenuInit(MenuCallbackArgs args)
        {
            CasusBelliCampaignBehavior main = GetMain();
            Settlement settlement = Settlement.CurrentSettlement;

            string text = main != null && settlement != null
                ? main.BuildSettlementStatus(settlement, GetPlayerKingdom())
                : "Casus Belli indisponivel.";

            MBTextManager.SetTextVariable("CB_STATUS", text);
        }

        // ---------------- Fabricar ----------------

        private bool FabricateCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            CasusBelliCampaignBehavior main = GetMain();
            Settlement settlement = Settlement.CurrentSettlement;
            Kingdom kingdom = GetPlayerKingdom();

            if (main == null || settlement == null || kingdom == null)
            {
                return false;
            }

            if (main.GetSettlementOwnerKingdom(settlement) == kingdom)
            {
                return false;
            }

            string reason;

            if (!IsPlayerRuler(kingdom))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(
                    "So o governante do reino fabrica reivindicacoes.");
            }
            else if (!main.CanStartFabrication(
                kingdom,
                settlement,
                false,
                out reason))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(reason);
            }

            return true;
        }

        private void FabricateConsequence(MenuCallbackArgs args)
        {
            CasusBelliCampaignBehavior main = GetMain();
            Settlement settlement = Settlement.CurrentSettlement;
            Kingdom kingdom = GetPlayerKingdom();

            if (main == null || settlement == null || kingdom == null)
            {
                return;
            }

            int gold;
            float influence;
            main.GetFabricationCost(kingdom, settlement, out gold, out influence);

            string text = "Custo: " + gold + " denarios e " +
                influence.ToString("0") + " de influencia. Prazo: " +
                CasusBelliCampaignBehavior.FabricationDays.ToString("0") +
                " dias.";

            InformationManager.ShowInquiry(new InquiryData(
                "Fabricar reivindicacao",
                text,
                true,
                true,
                "Fabricar",
                "Cancelar",
                () =>
                {
                    string result;
                    main.TryStartFabrication(
                        kingdom.StringId,
                        settlement.StringId,
                        false,
                        out result);
                    Show("CB: " + result);
                    GameMenu.SwitchToMenu(MenuId);
                },
                () => { }));
        }

        // ---------------- Declarar guerra ----------------

        private bool DeclareCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            CasusBelliCampaignBehavior main = GetMain();
            Settlement settlement = Settlement.CurrentSettlement;
            Kingdom kingdom = GetPlayerKingdom();

            if (main == null || settlement == null || kingdom == null)
            {
                return false;
            }

            if (main.GetSettlementOwnerKingdom(settlement) == kingdom)
            {
                return false;
            }

            if (!IsPlayerRuler(kingdom))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(
                    "So o governante do reino declara guerra.");
                return true;
            }

            Kingdom defender;
            ClaimRecord claim;
            string reason;

            if (!main.CheckClaimWar(
                kingdom,
                settlement,
                false,
                out defender,
                out claim,
                out reason))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(reason);
            }

            return true;
        }

        private void DeclareConsequence(MenuCallbackArgs args)
        {
            CasusBelliCampaignBehavior main = GetMain();
            Settlement settlement = Settlement.CurrentSettlement;
            Kingdom kingdom = GetPlayerKingdom();

            if (main == null || settlement == null || kingdom == null)
            {
                return;
            }

            string text = "Declarar guerra de reivindicacao por " +
                settlement.Name + "? Custo: " +
                CasusBelliCampaignBehavior.ClaimWarInfluenceCost.ToString("0") +
                " de influencia.";

            string vanillaMenu = VanillaMenuId();

            InformationManager.ShowInquiry(new InquiryData(
                "Guerra de reivindicacao",
                text,
                true,
                true,
                "Declarar",
                "Cancelar",
                () =>
                {
                    string result;
                    main.TryDeclareClaimWar(
                        kingdom.StringId,
                        settlement.StringId,
                        false,
                        out result);
                    Show("CB: " + result);

                    // Volta ao menu normal: se a guerra comecou, o proprio
                    // jogo decide o que fazer com o jogador no feudo inimigo.
                    GameMenu.SwitchToMenu(vanillaMenu);
                },
                () => { }));
        }

        // ---------------- Usurpacao ativa ----------------

        private bool UsurpCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            CasusBelliCampaignBehavior main = GetMain();
            Settlement settlement = Settlement.CurrentSettlement;
            Kingdom kingdom = GetPlayerKingdom();

            if (main == null || settlement == null || kingdom == null)
            {
                return false;
            }

            // So aparece para o reino que ocupa o feudo hoje.
            if (main.GetSettlementOwnerKingdom(settlement) != kingdom)
            {
                return false;
            }

            if (!IsPlayerRuler(kingdom))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(
                    "So o governante do reino pode usurpar.");
            }
            else if (!main.CanUsurp(kingdom, settlement))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(
                    "Usurpacao ativa indisponivel neste feudo.");
            }

            return true;
        }

        private void UsurpConsequence(MenuCallbackArgs args)
        {
            CasusBelliCampaignBehavior main = GetMain();
            Settlement settlement = Settlement.CurrentSettlement;

            if (main == null || settlement == null)
            {
                return;
            }

            InformationManager.ShowInquiry(new InquiryData(
                "Usurpar o direito de jure",
                "O direito de jure sobre " + settlement.Name +
                    " passara a ser do seu reino. Confirmar?",
                true,
                true,
                "Usurpar",
                "Cancelar",
                () =>
                {
                    string result;
                    main.TryPerformActiveUsurpation(settlement, out result);
                    Show("CB: " + result);
                    GameMenu.SwitchToMenu(MenuId);
                },
                () => { }));
        }

        // ---------------- Negociacao do soberano capturado ----------------

        // ---------------- Menu de guerra e paz (WarScore) ----------------

        public const string PeaceMenuId = "casus_belli_peace";

        private static CasusBelliWarScoreBehavior GetWarScore()
        {
            if (Campaign.Current == null)
            {
                return null;
            }

            return Campaign.Current.GetCampaignBehavior<
                CasusBelliWarScoreBehavior>();
        }

        // Chamado pelo botao WS da tela do mapa.
        public static void OpenPeaceMenuFromMap()
        {
            if (Campaign.Current == null || GetWarScore() == null)
            {
                return;
            }

            // Interface Gauntlet ativa: abre a janela de negociacao.
            if (CasusBelliWarScoreBehavior.WindowOpener != null)
            {
                CasusBelliWarScoreBehavior.WindowOpener();
                return;
            }

            if (Campaign.Current.CurrentMenuContext != null)
            {
                Show("CB: termine a acao atual ou use a opcao " +
                    "\"Casus Belli: guerra e paz\" no menu da cidade/castelo.");
                return;
            }

            GameMenu.ActivateGameMenu(PeaceMenuId);
        }

        private void AddPeaceEntryOption(CampaignGameStarter starter, string menuId)
        {
            starter.AddGameMenuOption(
                menuId,
                "casus_belli_peace_entry",
                "Casus Belli: guerra e paz",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

                    CasusBelliWarScoreBehavior ws = GetWarScore();
                    Kingdom kingdom = GetPlayerKingdom();

                    return ws != null &&
                        kingdom != null &&
                        ws.GetActiveWarsOf(kingdom).Count > 0;
                },
                args =>
                {
                    if (CasusBelliWarScoreBehavior.WindowOpener != null)
                    {
                        CasusBelliWarScoreBehavior.WindowOpener();
                    }
                    else
                    {
                        GameMenu.SwitchToMenu(PeaceMenuId);
                    }
                },
                false,
                2);
        }

        private void AddPeaceMenu(CampaignGameStarter starter)
        {
            starter.AddGameMenu(
                PeaceMenuId,
                "{CB_PEACE}",
                args => OnPeaceInit(args));

            starter.AddGameMenuOption(
                PeaceMenuId,
                "casus_belli_peace_enforce",
                "Forcar exigencias",
                args => PeaceActionCondition(args, 0),
                args => PeaceActionConsequence(0));

            starter.AddGameMenuOption(
                PeaceMenuId,
                "casus_belli_peace_white",
                "Paz branca",
                args => PeaceActionCondition(args, 1),
                args => PeaceActionConsequence(1));

            starter.AddGameMenuOption(
                PeaceMenuId,
                "casus_belli_peace_surrender",
                "Render-se",
                args => PeaceActionCondition(args, 2),
                args => PeaceActionConsequence(2));

            starter.AddGameMenuOption(
                PeaceMenuId,
                "casus_belli_peace_king",
                "Negociar com o soberano capturado",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

                    CasusBelliKingBehavior kings = GetKings();
                    KingCaptureRecord record;

                    return kings != null &&
                        kings.TryGetPlayerNegotiation(out record);
                },
                args => GameMenu.SwitchToMenu(NegotiationMenuId));

            starter.AddGameMenuOption(
                PeaceMenuId,
                "casus_belli_peace_next",
                "Outra guerra",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

                    CasusBelliWarScoreBehavior ws = GetWarScore();
                    Kingdom kingdom = GetPlayerKingdom();

                    return ws != null &&
                        kingdom != null &&
                        ws.GetActiveWarsOf(kingdom).Count > 1;
                },
                args =>
                {
                    GetWarScore().CycleSelectedWar(GetPlayerKingdom());
                    GameMenu.SwitchToMenu(PeaceMenuId);
                });

            starter.AddGameMenuOption(
                PeaceMenuId,
                "casus_belli_peace_back",
                "Voltar",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                args =>
                {
                    if (Settlement.CurrentSettlement != null)
                    {
                        GameMenu.SwitchToMenu(VanillaMenuId());
                    }
                    else
                    {
                        GameMenu.ExitToLast();
                    }
                },
                true);
        }

        private void OnPeaceInit(MenuCallbackArgs args)
        {
            CasusBelliWarScoreBehavior ws = GetWarScore();
            Kingdom kingdom = GetPlayerKingdom();
            WarRecord war = ws != null && kingdom != null
                ? ws.GetSelectedWar(kingdom)
                : null;

            string text = war != null
                ? ws.BuildWarText(war, kingdom)
                : "Seu reino nao esta em guerra.";

            MBTextManager.SetTextVariable("CB_PEACE", text);
        }

        // kind: 0 = exigencias, 1 = paz branca, 2 = rendicao
        private bool PeaceActionCondition(MenuCallbackArgs args, int kind)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            CasusBelliWarScoreBehavior ws = GetWarScore();
            Kingdom kingdom = GetPlayerKingdom();
            WarRecord war = ws != null && kingdom != null
                ? ws.GetSelectedWar(kingdom)
                : null;

            if (war == null)
            {
                return false;
            }

            if (!IsPlayerRuler(kingdom))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(
                    "So o governante do reino negocia a paz.");
                return true;
            }

            string reason;
            bool allowed;

            if (kind == 0)
            {
                allowed = ws.CanEnforce(war, kingdom, out reason);
            }
            else if (kind == 1)
            {
                allowed = ws.CanWhitePeace(war, kingdom, out reason);
            }
            else
            {
                allowed = ws.CanSurrender(war, kingdom, out reason);
            }

            if (!allowed)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(reason);
            }

            return true;
        }

        private void PeaceActionConsequence(int kind)
        {
            CasusBelliWarScoreBehavior ws = GetWarScore();
            Kingdom kingdom = GetPlayerKingdom();
            WarRecord war = ws != null && kingdom != null
                ? ws.GetSelectedWar(kingdom)
                : null;

            if (war == null)
            {
                return;
            }

            string title;
            string text;

            if (kind == 0)
            {
                title = "Forcar exigencias";
                text = ws.PreviewDemands(war, kingdom) + "\nConfirmar?";
            }
            else if (kind == 1)
            {
                title = "Paz branca";
                text = "Encerrar a guerra sem vencedor nem exigencias. Confirmar?";
            }
            else
            {
                title = "Render-se";
                text = "Render-se e aceitar as exigencias do vencedor " +
                    "(cessao do feudo-alvo ou indenizacao). Confirmar?";
            }

            InformationManager.ShowInquiry(new InquiryData(
                title,
                text,
                true,
                true,
                "Confirmar",
                "Cancelar",
                () =>
                {
                    string result;

                    if (kind == 0)
                    {
                        ws.TryEnforceDemands(war, kingdom, out result);
                    }
                    else if (kind == 1)
                    {
                        ws.TryWhitePeace(war, out result);
                    }
                    else
                    {
                        ws.TrySurrender(war, kingdom, out result);
                    }

                    Show("CB: " + result);
                    GameMenu.SwitchToMenu(PeaceMenuId);
                },
                () => { }));
        }

        private void AddNegotiationMenu(CampaignGameStarter starter)
        {
            starter.AddGameMenu(
                NegotiationMenuId,
                "{CB_NEGOTIATION}",
                args => OnNegotiationInit(args));

            AddOfferOption(
                starter,
                "casus_belli_offer_objective",
                "Exigir o objetivo de guerra",
                NegotiationOfferType.Objective);

            AddOfferOption(
                starter,
                "casus_belli_offer_moderate",
                "Pedir resgate moderado",
                NegotiationOfferType.RansomModerate);

            AddOfferOption(
                starter,
                "casus_belli_offer_heavy",
                "Exigir resgate pesado",
                NegotiationOfferType.RansomHeavy);

            AddOfferOption(
                starter,
                "casus_belli_offer_mercy",
                "Libertar por clemencia",
                NegotiationOfferType.Mercy);

            // [NOVA OFERTA 5/5] Um AddOfferOption por oferta nova, aqui.

            starter.AddGameMenuOption(
                NegotiationMenuId,
                "casus_belli_negotiation_back",
                "Decidir depois",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                args => LeaveNegotiation(),
                true);
        }

        private void AddOfferOption(
            CampaignGameStarter starter,
            string optionId,
            string text,
            NegotiationOfferType type)
        {
            starter.AddGameMenuOption(
                NegotiationMenuId,
                optionId,
                text,
                args => OfferCondition(args, type),
                args => OfferConsequence(type));
        }

        private void OnNegotiationInit(MenuCallbackArgs args)
        {
            CasusBelliKingBehavior kings = GetKings();
            KingCaptureRecord record;

            string text = kings != null && kings.TryGetPlayerNegotiation(out record)
                ? kings.BuildNegotiationText(record)
                : "Nenhum soberano capturado aguardando negociacao.";

            MBTextManager.SetTextVariable("CB_NEGOTIATION", text);
        }

        private bool OfferCondition(
            MenuCallbackArgs args,
            NegotiationOfferType type)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            CasusBelliKingBehavior kings = GetKings();
            KingCaptureRecord record;

            if (kings == null || !kings.TryGetPlayerNegotiation(out record))
            {
                return false;
            }

            NegotiationOffer offer =
                kings.GetOffers(record).Find(o => o.Type == type);

            if (offer == null)
            {
                return false;
            }

            if (!offer.IsEnabled)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(offer.DisabledReason);
            }
            else
            {
                args.Tooltip = new TextObject(offer.Detail);
            }

            return true;
        }

        private void OfferConsequence(NegotiationOfferType type)
        {
            CasusBelliKingBehavior kings = GetKings();
            KingCaptureRecord record;

            if (kings == null || !kings.TryGetPlayerNegotiation(out record))
            {
                LeaveNegotiation();
                return;
            }

            string result;
            kings.ExecuteOffer(record, type, out result);
            Show("CB: " + result);

            if (record.IsResolved)
            {
                LeaveNegotiation();
            }
            else
            {
                // recusado: continua na janela para tentar outra oferta
                GameMenu.SwitchToMenu(NegotiationMenuId);
            }
        }

        private static void LeaveNegotiation()
        {
            GameMenu.SwitchToMenu(PeaceMenuId);
        }
    }
}
