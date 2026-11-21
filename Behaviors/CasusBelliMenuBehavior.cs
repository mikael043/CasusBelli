using CasusBelli.Managers.Titles;
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

        private static Kingdom GetOwnerKingdom(Settlement settlement)
        {
            if (settlement == null || settlement.Owner == null)
            {
                return null;
            }

            return settlement.Owner.Clan?.Kingdom;
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
            AddPeaceEntryOption(starter, "town");
            AddPeaceEntryOption(starter, "castle");

            starter.AddGameMenuOption(
                "town",
                "casus_belli_titles_entry",
                "Títulos do Reino",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

                    Kingdom kingdom = GetPlayerKingdom();

                    // Aparece sempre em cidades ou castelos
                    return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsFortification;
                },
                args => GameMenu.SwitchToMenu(TitlesMenuId),
                false,
                3);

            starter.AddGameMenuOption(
                "castle",
                "casus_belli_titles_entry",
                "Títulos do Reino",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsFortification;
                },
                args => GameMenu.SwitchToMenu(TitlesMenuId),
                false,
                4);

            AddPeaceMenu(starter);
            AddNegotiationMenu(starter);
            AddTitlesMenu(starter);
        }

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
            FeudalTitle claim;
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

        public const string PeaceMenuId = "casus_belli_peace";
        public const string TitlesMenuId = "casus_belli_titles";

        private void AddTitlesMenu(CampaignGameStarter starter)
        {
            starter.AddGameMenu(
                TitlesMenuId,
                "{CB_TITLES}",
                args => OnTitlesInit(args));

            AddTitleAction(starter, ActionType.Claim, "Fabricar reivindicação sobre este título");
            starter.AddGameMenuOption(TitlesMenuId, "casus_belli_choose_claim", "Fabricar reivindicação sobre outro título…",
                args => true, args => UI.Titles.KingdomHierarchyTab.ChooseClaimTitle(() => GameMenu.SwitchToMenu(TitlesMenuId)));
            AddTitleAction(starter, ActionType.Usurp, "Usurpar este título");
            starter.AddGameMenuOption(TitlesMenuId, "casus_belli_declare", "Declarar guerra pelo título",
                DeclareCondition, DeclareConsequence);

            starter.AddGameMenuOption(
                TitlesMenuId,
                "casus_belli_titles_back",
                "Voltar",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                args => GameMenu.SwitchToMenu(VanillaMenuId()),
                true);
        }

        private static void AddTitleAction(CampaignGameStarter starter, ActionType type, string label)
        {
            TitleAction CurrentAction()
            {
                var title = TitleManager.Instance.GetTitle(Settlement.CurrentSettlement);
                return TitleManager.Instance.GetModel().GetAction(type, title, Hero.MainHero);
            }

            starter.AddGameMenuOption(TitlesMenuId, "casus_belli_title_" + type, label,
                args =>
                {
                    var action = CurrentAction();
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    args.IsEnabled = action.Possible;
                    args.Tooltip = action.Reason;
                    return true;
                },
                args =>
                {
                    var action = CurrentAction();
                    if (!action.Possible) return;
                    if (type == ActionType.Claim)
                    {
                        UI.Titles.KingdomHierarchyTab.ConfirmClaim(action.Title, () => GameMenu.SwitchToMenu(TitlesMenuId));
                        return;
                    }
                    string cost = $"{action.Title.FullName}\n{action.Gold:0} ouro, {action.Influence:0} influência, {action.Renown:0} renome.";
                    InformationManager.ShowInquiry(new InquiryData(label, cost, true, true, "Confirmar", "Cancelar",
                        () => { CurrentAction().TakeAction(null); GameMenu.SwitchToMenu(TitlesMenuId); }, () => { }));
                });
        }

        private void OnTitlesInit(MenuCallbackArgs args)
        {
            var fief = Settlement.CurrentSettlement;
            var title = TitleManager.Instance.GetTitle(fief);
            string status = GetMain()?.BuildSettlementStatus(fief, GetPlayerKingdom());
            if (title != null)
            {
                var parent = TitleManager.Instance.GetImmediateSuzerain(title);
                status += $"\nSuserano: {parent?.FullName}\nSoberano: {title.Sovereign?.FullName}";
            }
            MBTextManager.SetTextVariable("CB_TITLES", status ?? "Títulos indisponíveis.");
        }

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
                "Apresentar exigências",
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
                title = "Apresentar exigências";
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
                text = ws.PreviewDemands(war, ws.GetEnemyKingdom(war, kingdom)) + "\nConfirmar rendição?";
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
                        ws.TryWhitePeace(war, kingdom, out result);
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
                "Exigir o feudo",
                NegotiationOfferType.Objective);

            AddOfferOption(
                starter,
                "casus_belli_offer_moderate",
                "Resgatar o soberano e continuar a guerra",
                NegotiationOfferType.RansomModerate);

            AddOfferOption(
                starter,
                "casus_belli_offer_heavy",
                "Exigir indenização pela paz",
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

            kings.PlayerOffer(record, type, result =>
            {
                Show("CB: " + result);
                if (record.IsResolved) LeaveNegotiation();
                else GameMenu.SwitchToMenu(NegotiationMenuId);
            });
        }

        private static void LeaveNegotiation()
        {
            GameMenu.SwitchToMenu(PeaceMenuId);
        }
    }
}
