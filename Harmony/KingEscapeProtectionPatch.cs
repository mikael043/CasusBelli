// PROTECAO DO REI CONTRA FUGA (opcional, usa Harmony).
//
// Para ativar:
//  1. Adicione o modulo Bannerlord.Harmony como dependencia no SubModule.xml.
//  2. Referencie a biblioteca Harmony (0Harmony / Lib.Harmony) no projeto,
//     com Copiar Local = False.
//  3. Defina o simbolo de compilacao CB_HARMONY (Propriedades do projeto >
//     Compilar > Simbolos de compilacao condicional).
//  4. Confirme no ILSpy que a fuga de herois passa por
//     EndCaptivityAction.ApplyByEscape. Se o nome for outro, troque abaixo.
//
// Sem o simbolo CB_HARMONY este arquivo nao e compilado e o mod nao usa
// Harmony.
#if CB_HARMONY
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CasusBelli
{
    [HarmonyPatch(typeof(EndCaptivityAction), "ApplyByEscape")]
    public static class KingEscapeProtectionPatch
    {
        // __args evita depender do nome exato dos parametros do jogo.
        // Retornar false cancela a fuga.
        public static bool Prefix(object[] __args)
        {
            Hero hero = __args != null && __args.Length > 0
                ? __args[0] as Hero
                : null;

            if (hero == null || Campaign.Current == null)
            {
                return true;
            }

            CasusBelliKingBehavior kings =
                Campaign.Current.GetCampaignBehavior<CasusBelliKingBehavior>();

            if (kings != null && kings.IsEscapeBlocked(hero))
            {
                return false;
            }

            return true;
        }
    }
}
#endif
