using System;
using System.IO;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.Core;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CasusBelli.Managers.Titles;
using TaleWorlds.SaveSystem;
using TaleWorlds.Localization;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using System.Runtime.Serialization;
using System.Xml;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.TwoDimension;
class FixtureMovie : IGauntletMovie {
 public FixtureMovie(Widget root){RootWidget=root;}
 public Widget RootWidget {get;private set;} public string MovieName {get{return "KingdomManagement";}}
 public bool IsLoaded {get{return true;}} public bool IsReleased {get{return false;}}
 public void Update(){} public void Release(){} public void RefreshBindingWithChildren(){}
}
class Check { static Hero player; public static bool MainHero(ref Hero __result){__result=player;return false;}
 static string game = Environment.GetEnvironmentVariable("CB_TEST_GAME");
 static void Main() {
  AppDomain.CurrentDomain.AssemblyResolve += (o,e) => {
   var file=new AssemblyName(e.Name).Name+".dll";
   foreach(var root in new[]{AppDomain.CurrentDomain.BaseDirectory, game+"/bin/Win64_Shipping_Client",game+"/Modules/Bannerlord.Harmony/bin/Win64_Shipping_Client",game+"/Modules/Bannerlord.UIExtenderEx/bin/Win64_Shipping_Client",game+"/Modules/SandBox/bin/Win64_Shipping_Client"}) {
    var path=Path.Combine(root,file); if(File.Exists(path)) return Assembly.LoadFrom(path);
   }
   return null;
  };
  try { Run(); } catch(Exception e) { Console.WriteLine(e); Environment.Exit(1); }
 }
 [MethodImpl(MethodImplOptions.NoInlining)]
 static void Run(){
 var store=new RoyalStore();store.Data["CasusBelli_KingCaptureData_v1"]=new List<string>{"king","a","b","hero","1.5","False",""};
 var kings=new CasusBelli.CasusBelliKingBehavior();kings.SyncData(store);
 var cap=kings.AllCaptures.Single();if(cap.CapturedOnDay!=1.5||cap.Refusals!=0)throw new Exception("legacy capture");
 cap.NextAttempt=12.5;cap.Refusals=2;store.IsSaving=true;kings.SyncData(store);store.IsSaving=false;
 kings=new CasusBelli.CasusBelliKingBehavior();kings.SyncData(store);cap=kings.AllCaptures.Single();
 if(cap.NextAttempt!=12.5||cap.Refusals!=2)throw new Exception("royal terms save");
 Console.WriteLine("Legacy royal custody and negotiated terms save round trip passed");
 var behavior=new CasusBelli.CasusBelliCampaignBehavior();
 behavior.GetWarRegistry().AddWar(new CasusBelli.WarRecord{WarId="test",AttackerKingdomId="a",DefenderKingdomId="d",ArmisticeStarted=45,ArmisticeEnds=129,LastResumed=-1});
 var db=new RoyalStore();db.IsSaving=true;behavior.SyncData(db);db.IsSaving=false;
 behavior=new CasusBelli.CasusBelliCampaignBehavior();behavior.SyncData(db);
 var saved=behavior.GetWarRegistry().AllWars.Single();
 if(!saved.IsActive || saved.ArmisticeEnds!=129 || saved.ArmisticeStarted!=45)throw new Exception("armistice save");
 var kpatch=typeof(TitleManager).Assembly.GetType("CasusBelli.KingRansomProtectionPatch",true);
 new HarmonyLib.Harmony("cb.test.ransom").CreateClassProcessor(kpatch).Patch();
 Console.WriteLine("Armistice save/load and native ransom Harmony binding passed");
 foreach(var patch in new[]{"RaidVolunteerPatch","RaidRecruitmentPatch","RaidTavernPatch"})
 new HarmonyLib.Harmony("cb.test.raids").CreateClassProcessor(typeof(TitleManager).Assembly.GetType("CasusBelli."+patch,true)).Patch();
 Console.WriteLine("Native volunteer, party and tavern recruitment patches bound");
 foreach(var patch in new[]{"PeerProposalPatch","PeerFiefPatch","ElectoratePatch","NoblePreferencesPatch","BallotCountPatch","BallotResultPatch","LevyCostPatch","RoyalCandidatesPatch","BallotPreviewPatch"})
 new HarmonyLib.Harmony("cb.test.peers").CreateClassProcessor(typeof(TitleManager).Assembly.GetType("CasusBelli."+patch,true)).Patch();
 var peerDriver=new InMemDriver(); var peerSave=SaveManager.Save(new CasusBelli.PeerVote(true),new MetaData(),"peer",peerDriver);
 if(!peerSave.Successful || !((CasusBelli.PeerVote)SaveManager.Load("peer",peerDriver).Root).Approve)throw new Exception("peer vote save");
 Console.WriteLine("Peerage election patches and native vote save/load passed");
 var councilStore=new RoyalStore();councilStore.Data["CasusBelli_RoyalCouncil_v1"]=new List<string>{"realm|0|steward","realm|3|spy"};
 var council=new CasusBelli.RoyalCouncil();council.SyncData(councilStore);councilStore.IsSaving=true;council.SyncData(councilStore);
 if(!((List<string>)councilStore.Data["CasusBelli_RoyalCouncil_v1"]).Contains("realm|3|spy"))throw new Exception("council save");
 Console.WriteLine("Council seat persistence passed");
 foreach(var patch in new[]{"PlayerMarshalPermission","LordMarshalPermission","KingdomMarshalPermission","MarshalGatherPermission","MarshalArmyOrder","DucalArmyCandidates","DucalArmySelection"})
 new HarmonyLib.Harmony("cb.test.marshal").CreateClassProcessor(typeof(TitleManager).Assembly.GetType("CasusBelli."+patch,true)).Patch();
 Console.WriteLine("Royal and ducal army command patches bound");
 var calls=new RoyalStore();calls.Data["CasusBelli_NextArmySummons_v1"]=12.5;
 var summons=new CasusBelli.ArmySummonsBehavior();summons.SyncData(calls);calls.IsSaving=true;summons.SyncData(calls);
 if((double)calls.Data["CasusBelli_NextArmySummons_v1"]!=12.5)throw new Exception("summons cooldown");
 var marshalType=typeof(TitleManager).Assembly.GetType("CasusBelli.MarshalCommand",true);
 var serviceDays=marshalType.GetMethod("ServiceDaysForLevyLevel",BindingFlags.Static|BindingFlags.NonPublic);
 Require((int)serviceDays.Invoke(null,new object[]{0})==10 && (int)serviceDays.Invoke(null,new object[]{1})==10 &&
   (int)serviceDays.Invoke(null,new object[]{2})==20 && (int)serviceDays.Invoke(null,new object[]{3})==30 &&
   (int)serviceDays.Invoke(null,new object[]{4})==40 && (int)serviceDays.Invoke(null,new object[]{99})==40,
   "Military service duration must follow levy law levels");
 var summonsQuestType=typeof(TitleManager).Assembly.GetType("CasusBelli.ArmySummonsQuest",true);
 var earlyDeparture=summonsQuestType.GetMethod("ShouldPunishEarlyDeparture",BindingFlags.Static|BindingFlags.NonPublic);
 Func<bool,bool,bool,bool> desert=(started,valid,inArmy)=>(bool)earlyDeparture.Invoke(null,new object[]{started,valid,inArmy});
 Require(desert(true,true,false)&&!desert(false,true,false)&&!desert(true,false,false)&&!desert(true,true,true),
   "Military service desertion only while an active valid obligation exists");
 Console.WriteLine("Feudal military service: levy duration and early-departure rules passed");
 // WarScore: ocupacao perdida tem de reiniciar a contagem em reconquista.
 var cbType=typeof(TitleManager).Assembly.GetType("CasusBelli.CasusBelliCampaignBehavior",true);
 var occupation=cbType.GetMethod("NextObjectiveOccupationDay",BindingFlags.Static|BindingFlags.NonPublic);
 Func<double,bool,double,double> objective=(prior,holds,now)=>(double)occupation.Invoke(null,new object[]{prior,holds,now});
 Require(objective(-1,true,10)==10 && objective(10,true,20)==10 &&
   objective(10,false,21)==-1 && objective(-1,true,30)==30 &&
   objective(30,false,31)==-1,"WarScore target occupation must reset on recapture");
 // Feridos, inferioridade numerica e qualidade da vitoria; teto por batalha.
 var warScoreType=typeof(TitleManager).Assembly.GetType("CasusBelli.CasusBelliWarScoreBehavior",true);
 var battlePoints=warScoreType.GetMethod("CalculateBattlePoints",BindingFlags.Static|BindingFlags.NonPublic);
 Func<int,int,int,int,int,int,double,double> battle=(ld,lw,wd,ww,ws,ls,kingdom)=>
   (double)battlePoints.Invoke(null,new object[]{ld,lw,wd,ww,ws,ls,kingdom});
 double baseline=battle(100,0,20,0,500,500,2000);
 Require(baseline>0 && battle(100,60,20,0,500,500,2000)>baseline,
   "WarScore: wounded troops must count half weight");
 Require(battle(100,0,20,0,250,500,2000)>baseline &&
   battle(100,0,20,0,1000,500,2000)<baseline,
   "WarScore: outnumbered victory should have higher weight");
 Require(battle(100,0,0,0,500,500,2000)>battle(100,0,100,0,500,500,2000),
   "WarScore: cleaner wins should have higher weight");
 Require(battle(0,4,0,0,500,500,2000)==0 &&
   Math.Abs(battle(2000,100,0,0,500,500,1500)-8)<.00001,
   "WarScore: minimum casualties and per-battle cap");
 Console.WriteLine("WarScore: occupation reset, weighted casualties and battle caps passed");
 var manager=TitleManager.Instance;
 var contract=(FeudalContract)typeof(TitleGenerator).GetMethod("GenerateContract",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(string)},null).Invoke(null,new object[]{"feudal"});
 var title=new FeudalTitle(TitleType.Dukedom,null,null,null,null,new TextObject("Sargotha"),contract,"sargotha");
 var root=new FeudalTitle(TitleType.Kingdom,null,new List<FeudalTitle>{title},null,null,new TextObject("Vlandia"),contract,"vlandia");
 var add=typeof(TitleManager).GetMethod("ExecuteAddTitle",BindingFlags.Instance|BindingFlags.NonPublic);
 add.Invoke(manager,new object[]{root});add.Invoke(manager,new object[]{title});manager.Initialize();
 var driver=new InMemDriver();
 var output=SaveManager.Save(manager,new MetaData(),"test",driver);
 if(!output.Successful){ foreach(var err in output.Errors) Console.WriteLine(err.Message); throw new Exception("save failed"); }
 var result=SaveManager.Load("test",driver);if(!result.Successful)throw new Exception("load failed");
 var restored=(TitleManager)result.Root;
 var duchy=restored.GetTitleByStringId("sargotha");var sovereign=restored.GetTitleByStringId("vlandia");
 if(duchy==null || duchy.Sovereign!=sovereign || sovereign.Vassals.Single()!=duchy || duchy.Contract.Government.StringId!="feudal")throw new Exception("graph mismatch");
 Console.WriteLine("Native save/load round trip passed"); World();
 }
 static T Blank<T>() {return (T)FormatterServices.GetUninitializedObject(typeof(T));}
 static void Field(object obj,string name, object v){
  obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(obj,v);
 }
 static Dictionary<Hero,float> fixtureAges;
 public static bool FixtureAge(Hero __instance,ref float __result){if(!fixtureAges.TryGetValue(__instance,out __result))return true;return false;}
 static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static void PoliticalChecks(Campaign campaign,MBList<Kingdom> realms,MBList<Hero> heroes,TitleManager titles,XmlDocument xml){
  var asm=typeof(TitleManager).Assembly;
  var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  var politics=new CasusBelli.RealmPolitics();
  var bm=typeof(Campaign).Assembly.GetType("TaleWorlds.CampaignSystem.CampaignBehaviors.CampaignBehaviorManager",true);
  var mgr=FormatterServices.GetUninitializedObject(bm);
  bm.GetField("_campaignBehaviors",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(mgr,new List<CampaignBehaviorBase>{politics,new CasusBelli.RealmPeers()});
  Field(campaign,"_campaignBehaviorManager",mgr);
  var kingdom=realms[0];
  var ruler=kingdom.Leader;
  var model=politics.GetType();
  var store=new RoyalStore();
  model.GetMethod("Shift",flags).Invoke(null,new object[]{kingdom,-1000});
  Require((int)model.GetMethod("Authority",flags).Invoke(null,new[]{kingdom})==0,"Authority floor");
  model.GetMethod("Conduct",flags).Invoke(null,new object[]{ruler,kingdom,-9,11});
  store.IsSaving=true;politics.SyncData(store);store.IsSaving=false;
  var restored=new CasusBelli.RealmPolitics();restored.SyncData(store);
  Require(((List<string>)store.Data["CasusBelli_PoliticalRegister_v1"]).Count==3,"Register persistence");
  Require((string)model.GetMethod("Read",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(restored,new[]{"scandal|"+ruler.StringId})=="11","Controversy load");
  var root=(XmlElement)xml.SelectSingleNode("//titles").CloneNode(true);
  var county=(XmlElement)root.SelectSingleNode("kingdom/duchy/county");
  county.ParentNode.AppendChild(county.CloneNode(true));
  var rules=asm.GetType("CasusBelli.Managers.Titles.FeudalRules",true);
  try { rules.GetMethod("Validate",flags).Invoke(null,new object[]{root});throw new Exception("Duplicate accepted"); }
  catch(TargetInvocationException ex){if(!(ex.InnerException is InvalidDataException))throw;}
  var src=titles.GetTitleByStringId("town_V1").Contract;
  county.SetAttribute("inheritance","ultimogeniture");
  var separate=(FeudalContract)rules.GetMethod("Contract",flags).Invoke(null,new object[]{county,src});
  Require(separate.Inheritance.StringId=="ultimogeniture" && src.Inheritance.StringId!="ultimogeniture","Local succession law");
  var legal=titles.GetTitleByStringId("town_V1");
  var realm2=titles.GetSovereignTitle(realms[1]);
  legal.TickDrift(realm2,.51f);legal.TickDrift(realm2,.51f);
  Require(legal.Sovereign==realm2 && legal.DeJureDrifts.Count==0 && realm2.Vassals.Contains(legal),"Jurisdiction transfer");
  var decision=Blank<CasusBelli.PeerageDecision>();
  typeof(TaleWorlds.CampaignSystem.Election.KingdomDecision).GetField("_kingdom",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(decision,kingdom);
  var clan=ruler.Clan;Field(clan,"_heroesCache",new MBList<Hero>{ruler});Field(clan,"_fiefsCache",new MBList<Town>());
  var peer=Blank<Clan>();peer.StringId="test_peer";Field(peer,"_kingdom",kingdom);Field(peer,"_leader",heroes[1]);Field(peer,"_heroesCache",new MBList<Hero>());Field(peer,"_fiefsCache",new MBList<Town>());
  var merc=Blank<Clan>();Field(merc,"_kingdom",kingdom);Field(merc,"_leader",heroes[2]);Field(merc,"<IsUnderMercenaryService>k__BackingField",true);
  Field(kingdom,"_clans",new MBList<Clan>{clan,peer,merc});
  var electorate=decision.DetermineSupporters().ToList();
  Require(electorate.Count==2 && electorate.All(s=>s.Clan!=merc),"Election eligibility");
  var a=new CasusBelli.PeerVote(true);var b=new CasusBelli.PeerVote(false);
  a.SupporterList.Add(new TaleWorlds.CampaignSystem.Election.Supporter(clan){SupportWeight=TaleWorlds.CampaignSystem.Election.Supporter.SupportWeights.SlightlyFavor});
  b.SupporterList.Add(new TaleWorlds.CampaignSystem.Election.Supporter(peer){SupportWeight=TaleWorlds.CampaignSystem.Election.Supporter.SupportWeights.SlightlyFavor});
  var election=Blank<TaleWorlds.CampaignSystem.Election.KingdomElection>();
  Field(election,"_decision",decision);Field(election,"_possibleOutcomes",new MBList<TaleWorlds.CampaignSystem.Election.DecisionOutcome>{a,b});
  election.GetType().GetMethod("DetermineOfficialSupport",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(election,null);
  Require(a.TotalSupportPoints==3 && b.TotalSupportPoints==1 && a.WinChance==.75f,"Territorial votes");
  fixtureAges=new Dictionary<Hero,float>();
  typeof(TaleWorlds.CampaignSystem.Election.KingdomDecision).GetProperty("ProposerClan").GetSetMethod(true).Invoke(decision,new object[]{clan});
  var select=asm.GetType("CasusBelli.BallotResultPatch",true).GetMethod("Prefix",flags);
  object[] ballot={election,decision,b};select.Invoke(null,ballot);
  Require(ballot[2]==a,"Binding majority");
  model.GetMethod("Shift",flags).Invoke(null,new object[]{kingdom,65});
  ballot[2]=b;select.Invoke(null,ballot);
  Require(ballot[2]==b && (int)model.GetMethod("Controversy",flags).Invoke(null,new object[]{ruler})==19,"Override sanction");
  var heirs=new List<Hero>();
  for(int i=0;i<3;i++){
    var child=Blank<Hero>();child.StringId="heir_"+i;Field(child,"_father",ruler);Field(child,"_clan",clan);Field(child,"<Occupation>k__BackingField",Occupation.Lord);
    child.IsFemale=(i==0);fixtureAges[child]=35-i*5;heirs.Add(child);
  }
  new HarmonyLib.Harmony("cb.test.ages").Patch(typeof(Hero).GetProperty("Age").GetGetMethod(),prefix:new HarmonyLib.HarmonyMethod(typeof(Check).GetMethod("FixtureAge")));
  Field(clan,"_heroesCache",new MBList<Hero>(heirs));
  var personal=rules.GetMethod("PersonalHeir",flags);
  Func<string,string,Hero> inherit=(inheritance,gender)=>{
    county.SetAttribute("inheritance",inheritance);county.SetAttribute("genderLaw",gender);
    var contract=(FeudalContract)rules.GetMethod("Contract",flags).Invoke(null,new object[]{county,src});
    return (Hero)personal.Invoke(null,new object[]{ruler,new FeudalTitle(TitleType.County,null,null,ruler,ruler,new TextObject("Feudo"),contract,"test"),1});
  };
  Require(inherit("primogeniture","cognatic")==heirs[0] && inherit("primogeniture","agnatic")==heirs[1] &&
    inherit("ultimogeniture","agnatic")==heirs[2] && inherit("gavelkind","agnatic")==heirs[2],"Individual heirs");
  var crown=titles.GetSovereignTitle(kingdom);
  typeof(CasusBelli.Managers.Titles.TitleCampaignBehavior).GetMethod("OnHeroKilled",BindingFlags.NonPublic|BindingFlags.Instance)
    .Invoke(new CasusBelli.Managers.Titles.TitleCampaignBehavior(),new object[]{ruler,null,false});
  Require(crown.deJure==ruler && legal.deJure==heirs[1],"Crown and personal succession");
  Field(clan,"_leader",heirs[2]);
  typeof(CasusBelli.Managers.Titles.TitleCampaignBehavior).GetMethod("SyncCrown",flags).Invoke(null,new object[]{kingdom});
  Require(crown.deJure==heirs[2] && legal.deJure==heirs[1],"Crown after ruler change");
  var movement=(CasusBelli.Disputes.PoliticalMovement)Activator.CreateInstance(typeof(CasusBelli.Disputes.PoliticalMovement),
    BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{null,null,"teste"},null);
  var driver=new InMemDriver();
  Require(SaveManager.Save(movement,new MetaData(),"movement",driver).Successful,"Movement save");
  var state=(CasusBelli.Disputes.PoliticalMovement)SaveManager.Load("movement",driver).Root;
  Require(state.Demand=="teste" && state.Offers!=null,"Movement load");
  Console.WriteLine("Individual inheritance, ruler death, separate election of the crown and coalition save passed");
  Console.WriteLine("Political register, title overrides, XML rejection, jurisdiction and native weighted election passed");
 }
 static void World(){
  typeof(TitleManager).GetMethod("BeginCampaign",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
  var campaign=Blank<Campaign>(); typeof(Campaign).GetProperty("Current").GetSetMethod(true).Invoke(null,new object[]{campaign});
  var db=Blank<CampaignObjectManager>();Field(campaign,"<CampaignObjectManager>k__BackingField",db);
  var kingdoms=new MBList<Kingdom>();var alive=new MBList<Hero>();Field(db,"_kingdoms",kingdoms);Field(db,"_aliveHeroes",alive);Field(db,"_deadOrDisabledHeroes",new MBList<Hero>());
  var om=MBObjectManager.Init();om.RegisterType<Settlement>("Settlement","Settlements",1);
  var doc=new XmlDocument();doc.Load(Environment.GetEnvironmentVariable("CB_TEST_TITLES"));
  foreach(XmlElement node in doc.SelectNodes("//kingdom")){
   var k=Blank<Kingdom>();k.StringId=node.GetAttribute("faction");Field(k,"<Name>k__BackingField",new TextObject(k.StringId));kingdoms.Add(k);
   var clan=Blank<Clan>();Field(clan,"_kingdom",k);Field(k,"_rulingClan",clan);
   foreach(XmlElement n in node.SelectNodes("descendant-or-self::*")){
    var id=n.GetAttribute("deJure");var hero=alive.FirstOrDefault(x=>x.StringId==id);if(hero==null){hero=Blank<Hero>();hero.StringId=id;alive.Add(hero);Field(hero,"_clan",clan);}
    if(n==node) Field(clan,"_leader",hero);
    if(!n.HasAttribute("settlement"))continue;
    var f=Blank<Settlement>();f.StringId=n.GetAttribute("settlement");Field(f,"_name",new TextObject(f.StringId));Field(f,"<Party>k__BackingField",Blank<PartyBase>());f.Town=Blank<Town>();Field(f.Town,"_ownerClan",clan);Field(f.Town,"_isCastle",n.Name=="barony");om.RegisterObject(f);
   }
  }
  var extra=Blank<Settlement>(); extra.StringId="town_test";Field(extra,"_name",new TextObject("Test")); Field(extra,"<Party>k__BackingField",Blank<PartyBase>());extra.Town=Blank<Town>();Field(extra.Town,"_ownerClan",kingdoms[0].RulingClan);om.RegisterObject(extra);
  Field(db,"<Settlements>k__BackingField",om.GetObjectTypeList<Settlement>());
  var m=TitleManager.Instance; var load=typeof(TitleManager).GetMethod("SeedFromFile",BindingFlags.Instance|BindingFlags.NonPublic);
  load.Invoke(m,new object[]{new Uri(doc.BaseURI).LocalPath});load.Invoke(m,new object[]{new Uri(doc.BaseURI).LocalPath});
  if(m.GetTitle(extra)==null || m.GetTitle(extra).Sovereign==null || m.GetAllTitlesByType(TitleType.Kingdom).Count!=doc.SelectNodes("//kingdom").Count || m.GetAllTitlesByType(TitleType.Dukedom).Count!=doc.SelectNodes("//duchy").Count || m.GetAllTitlesByType(TitleType.County).Count!=doc.SelectNodes("//county").Count+1 || m.GetAllTitlesByType(TitleType.Barony).Count!=doc.SelectNodes("//barony").Count)throw new Exception("Seeding or duplicate error");
  var has=typeof(CasusBelli.CasusBelliCampaignBehavior).GetMethod("HasTitleRight",BindingFlags.Static|BindingFlags.NonPublic);
  var t0=m.GetTitle(extra); if(!(bool)has.Invoke(null,new object[]{kingdoms[0],t0}) || (bool)has.Invoke(null,new object[]{kingdoms[1],t0})) throw new Exception("de jure rights");
  var claimant=kingdoms[1].Leader;t0.OngoingClaims[claimant]=default(CampaignTime);
  if((bool)has.Invoke(null,new object[]{kingdoms[1],t0})) throw new Exception("pending claim allowed");
  t0.Claims[claimant]=ClaimType.Fabricated;
  if(!(bool)has.Invoke(null,new object[]{kingdoms[1],t0})) throw new Exception("mature claim rejected");
  Console.WriteLine("Repeated initialization, auto-generated fief and title war rights passed");
  var hierarchyType=typeof(TitleManager).Assembly.GetType("CasusBelli.UI.Titles.FeudalHierarchyTree",true);
  var hierarchyFlags=BindingFlags.NonPublic|BindingFlags.Instance;
  var buildHierarchy=hierarchyType.GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic);
  var showHierarchy=hierarchyType.GetMethod("Visible",hierarchyFlags);
  Func<object,HashSet<FeudalTitle>,List<object>> showTree=(tree,expanded)=>{
    var list=new List<object>();
    foreach(var node in (System.Collections.IEnumerable)showHierarchy.Invoke(tree,new object[]{expanded}))list.Add(node);
    return list;
  };
  var everyTitle=(IEnumerable<FeudalTitle>)typeof(TitleManager)
    .GetProperty("AllTitles",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
    .GetValue(m);
  var vlandiaTree=buildHierarchy.Invoke(null,new object[]{kingdoms[0],false,
    m.GetSovereignTitleList(kingdoms[0]),everyTitle});
  var allVlandia=(IEnumerable<FeudalTitle>)hierarchyType.GetProperty("Titles",hierarchyFlags).GetValue(vlandiaTree);
  var complete=showTree(vlandiaTree,new HashSet<FeudalTitle>(allVlandia));
  var titleField=complete[0].GetType().GetField("Title",hierarchyFlags);
  var depthField=complete[0].GetType().GetField("Depth",hierarchyFlags);
  Require(complete.Count==allVlandia.Count() && complete.Count>1,
    "Full hierarchy should expose every title exactly once");
  Require(complete.Any(n=>((FeudalTitle)titleField.GetValue(n)).StringId=="town_V1") &&
    complete.Any(n=>((FeudalTitle)titleField.GetValue(n)).StringId=="castle_V7"),
    "Full hierarchy must include county and barony branches");
  var l0=new FeudalTitle(TitleType.Lordship,null,null,null,null,new TextObject("L"),null,"cb_lordship_test");
  var b0=new FeudalTitle(TitleType.Barony,null,new List<FeudalTitle>{l0},null,null,new TextObject("B"),null,"cb_barony_test");
  var c0=new FeudalTitle(TitleType.County,null,new List<FeudalTitle>{b0},null,null,new TextObject("C"),null,"cb_county_test");
  var d0=new FeudalTitle(TitleType.Dukedom,null,new List<FeudalTitle>{c0},null,null,new TextObject("D"),null,"cb_duchy_test");
  var k0=new FeudalTitle(TitleType.Kingdom,null,new List<FeudalTitle>{d0},null,null,new TextObject("K"),null,"cb_kingdom_test");
  var chain=new List<FeudalTitle>{k0,d0,c0,b0,l0};
  var chainTree=buildHierarchy.Invoke(null,new object[]{kingdoms[0],false,new[]{k0},chain});
  var chainRows=showTree(chainTree,new HashSet<FeudalTitle>(chain));
  Require(chainRows.Count==5 && chainRows.Select(n=>(int)depthField.GetValue(n)).SequenceEqual(new[]{0,1,2,3,4}),
    "Kingdom, duchy, county, barony and lordship navigation");
  Require(showTree(chainTree,new HashSet<FeudalTitle>{k0,d0}).Count==3,
    "Collapsing a hierarchy branch must hide descendants");
  Console.WriteLine("Full hierarchy: all titles, five levels and collapse/expand passed");
  foreach(XmlElement n in doc.SelectNodes("//*[@settlement]")){
   var f=Settlement.Find(n.GetAttribute("settlement"));var t=m.GetTitle(f);
   if(t==null || t.deJure.StringId!=n.GetAttribute("deJure") || t.TitleType!=(n.Name=="county"?TitleType.County:TitleType.Barony) || t.Sovereign==null || m.GetImmediateSuzerain(t)==null)throw new Exception("Invalid hierarchy "+f.StringId);
  }
  var priorLegal=t0.deJure;var priorClan=extra.Town.OwnerClan;
  Field(extra.Town,"_ownerClan",kingdoms[1].RulingClan);
  typeof(TitleManager).Assembly.GetType("CasusBelli.RoyalSettlement").GetMethod("ConfirmPossession",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{extra});
  if(t0.deFacto!=kingdoms[1].Leader || t0.deJure!=priorLegal || priorLegal==t0.deFacto)throw new Exception("Posse de facto");
  var occupiedTree=buildHierarchy.Invoke(null,new object[]{kingdoms[1],true,
    m.GetSovereignTitleList(kingdoms[1]),everyTitle});
  var occupiedTitles=(IEnumerable<FeudalTitle>)hierarchyType.GetProperty("Titles",hierarchyFlags).GetValue(occupiedTree);
  Require(occupiedTitles.Contains(t0),"De facto hierarchy must include occupied foreign fiefs");
  var occupationRows=showTree(occupiedTree,new HashSet<FeudalTitle>(occupiedTitles));
  Require(occupationRows.Any(n=>((FeudalTitle)titleField.GetValue(n))==t0 &&
    (int)depthField.GetValue(n)>=1),"Occupied title must attach under the effective kingdom");
  Field(extra.Town,"_ownerClan",priorClan);
  m.ApplyOwnerChange(extra,priorClan.Leader);
  Console.WriteLine("Peace possession updated de facto and retained original de jure holder");
  var asm=typeof(TitleManager).Assembly;
  var heroType=asm.GetType("CasusBelli.Harmony.TitleDisplayNamePatch",true);
  new HarmonyLib.Harmony("cb.integration.names").CreateClassProcessor(heroType).Patch();
  var h=kingdoms[0].Leader;var raw=new TextObject("Derthert");Field(h,"_name",raw);
  var getter=typeof(Hero).GetProperty("Name").GetGetMethod();
  Func<Hero,string> rendered=x=>getter.Invoke(x,null).ToString();
  if(rendered(h)!="Rei Derthert" || rendered(h)!="Rei Derthert" || raw.ToString()!="Derthert")throw new Exception("Rank prefix: "+rendered(h));
  h.IsFemale=true;if(rendered(h)!="Rainha Derthert")throw new Exception("Gender prefix");
  foreach(var realm in kingdoms){
   var c=Blank<CultureObject>();c.StringId=realm.StringId.StartsWith("empire") ? "empire" : realm.StringId;typeof(Kingdom).GetProperty("Culture").GetSetMethod(true).Invoke(realm,new object[]{c});
   var sovereignHero=realm.Leader;sovereignHero.IsFemale=false;Field(sovereignHero,"_name",new TextObject("Teste"));
   var names=new Dictionary<string,string>{{"vlandia","Rei Teste"},{"battania","Alto-Rei Teste"},{"sturgia","Grão-Príncipe Teste"},{"aserai","Sultão Teste"},{"empire","Imperador Teste"},{"khuzait","Teste Khan"},{"nord","Rei Teste"}};
   if(rendered(sovereignHero)!=names[c.StringId]) throw new Exception(rendered(sovereignHero));
  }
  Console.WriteLine("Cultural sovereign names passed");
  var outsider=Blank<Hero>();Field(outsider,"_name",raw);if(rendered(outsider)!="Derthert")throw new Exception("Untitled hero");
  var hierarchyVmType=asm.GetType("CasusBelli.UI.Titles.KingdomHierarchyVM",true);
  var hierarchyVm=Activator.CreateInstance(hierarchyVmType,
    BindingFlags.Instance|BindingFlags.NonPublic,null,
    new object[]{new Func<Kingdom>(()=>kingdoms[0])},null);
  hierarchyVmType.GetMethod("ExecuteOpen").Invoke(hierarchyVm,null);
  Require((bool)hierarchyVmType.GetProperty("IsOpen").GetValue(hierarchyVm),
    "Prefab hierarchy VM should open");
  Require((string)hierarchyVmType.GetProperty("RealmName").GetValue(hierarchyVm)==kingdoms[0].Name.ToString(),
    "Prefab hierarchy should use selected kingdom");
  Require(((string)hierarchyVmType.GetProperty("CountsText").GetValue(hierarchyVm)).Contains("ducados"),
    "Prefab hierarchy should expose rank counts");
  player=h;
  var harmony=new HarmonyLib.Harmony("cb.test.claims"); harmony.Patch(typeof(Hero).GetProperty("MainHero").GetGetMethod(), prefix:new HarmonyLib.HarmonyMethod(typeof(Check).GetMethod("MainHero")));
  var track=typeof(Campaign).GetProperty("MapTimeTracker",BindingFlags.NonPublic|BindingFlags.Instance);
  track.GetSetMethod(true).Invoke(campaign,new[]{FormatterServices.GetUninitializedObject(track.PropertyType)});
  foreach(var unit in new[]{"Day","Year"}){var tf=typeof(CampaignTime).GetField("TimeTicksPer"+unit,BindingFlags.Static|BindingFlags.NonPublic);tf.SetValue(null,unit=="Day"?1000L:84000L);}
  Field(h,"_gold",10000);Field(h.Clan,"_influence",200f);h.Clan.Renown=200;
  var target=m.GetTitleByStringId("castle_EN5"); var idList=new List<string>{target.StringId};
  if(!m.GetModel().GetClaimAction(target,h).Possible)throw new Exception("Cannot claim foreign title");
  target.AddOngoingClaim(h);target.AddOngoingClaim(claimant);
  string partial=CasusBelli.TitleDebugCommands.AdvanceClaim(new List<string>{target.StringId,"30"});
  if(target.OngoingClaims[h].RemainingDaysFromNow!=54 || target.OngoingClaims[claimant].RemainingDaysFromNow!=84)throw new Exception(partial);
  if(!CasusBelli.TitleDebugCommands.PendingClaims(null).Contains(target.StringId))throw new Exception("List pending");
  var finished=CasusBelli.TitleDebugCommands.FinishClaim(idList);
  if(!target.HeroHasValidClaim(h)||target.OngoingClaims.ContainsKey(h)||!target.OngoingClaims.ContainsKey(claimant))throw new Exception(finished);
  if(m.GetModel().GetClaimAction(target,h).Possible)throw new Exception("Duplicate claim allowed");
  Console.WriteLine("Player-only advance, listing, instant maturity and duplicate guard passed");
  var model=m.GetModel();
  var duchy=m.GetAllTitlesByType(TitleType.Dukedom).Where(d=>d.deJure.Clan.Kingdom!=h.Clan.Kingdom && d.Vassals.Any(v=>v.TitleType==TitleType.County))
    .OrderByDescending(d=>d.Vassals.Count(v=>v.TitleType==TitleType.County)).First();
  var counties=duchy.Vassals.Where(v=>v.TitleType==TitleType.County).ToList();
  var owners=counties.Select(v=>v.Fief.Town.OwnerClan).ToList();
  Field(h.Clan,"_influence",300f);h.Clan.Renown=500;
  duchy.Claims[h]=ClaimType.Fabricated;
  Require(!model.GetUsurpAction(duchy,h).Possible,"Mature claim without land must not transfer a duchy");
  for(int i=0;i<counties.Count/2;i++)Field(counties[i].Fief.Town,"_ownerClan",h.Clan);
  Require(!model.GetUsurpAction(duchy,h).Possible,"Half or less must not transfer a duchy even with mature claim");
  for(int i=0;i<counties.Count;i++)Field(counties[i].Fief.Town,"_ownerClan",h.Clan);
  Require(model.GetUsurpAction(duchy,h).Possible,"Territorial majority with mature claim");
  duchy.RemoveClaim(h);
  Require(model.GetUsurpAction(duchy,h).Possible,"Foreign territorial majority without claim");
  var originalRealm=h.Clan.Kingdom;
  Field(h.Clan,"_kingdom",duchy.deJure.Clan.Kingdom);
  Require(!model.GetUsurpAction(duchy,h).Possible,"Internal usurpation without mature claim must remain blocked");
  duchy.Claims[h]=ClaimType.Fabricated;
  Require(model.GetUsurpAction(duchy,h).Possible,"Internal majority with mature claim");
  duchy.RemoveClaim(h);
  Field(h.Clan,"_kingdom",originalRealm);
  for(int i=0;i<counties.Count;i++)Field(counties[i].Fief.Town,"_ownerClan",owners[i]);
  Console.WriteLine("Ducal usurpation: mature claim alone, half control and foreign majority regression checks passed");
  var disputeAi=asm.GetType("CasusBelli.Disputes.DisputeAi",true);
  var standing=disputeAi.GetMethod("HasDucalSponsorStanding",BindingFlags.Static|BindingFlags.NonPublic);
  Func<int,int,bool,bool,bool> canSponsor=(peer,tier,land,former)=>(bool)standing.Invoke(null,new object[]{peer,tier,land,former});
  Require(!canSponsor(1,6,true,false),"Minor peer cannot sponsor ducal dispute");
  Require(!canSponsor(2,2,true,false),"Unestablished clan cannot sponsor ducal dispute");
  Require(!canSponsor(2,6,false,false),"Landless newcomer cannot sponsor ducal dispute");
  Require(canSponsor(2,3,true,false)&&canSponsor(2,3,false,true),"Landed peer or established former holder");
  var threshold=disputeAi.GetMethod("DucalSupportThreshold",BindingFlags.Static|BindingFlags.NonPublic);
  Func<float,float,bool> support=(merit,relation)=>(bool)threshold.Invoke(null,new object[]{merit,relation});
  Require(!support(9,1000)&&!support(0,1000),"Friendship cannot bypass structural threshold");
  Require(!support(10,-1000)&&support(10,15)&&support(20,0),"Political interest and bounded relations");
  var sponsorBlock=disputeAi.GetMethod("DucalSponsorBlock",BindingFlags.Static|BindingFlags.NonPublic);
  duchy.Claims[h]=ClaimType.Fabricated;
  Require(sponsorBlock.Invoke(null,new object[]{duchy,h})!=null,"Foreign lord cannot recruit in a domestic dispute");
  duchy.RemoveClaim(h);
  Console.WriteLine("Ducal support: peerage, clan standing, landless exception, foreign claimant and bounded relations passed");
  var siegeReportType=asm.GetType("CasusBelli.CasusBelliSiegeReportVM",true);
  var siegeReport=Activator.CreateInstance(siegeReportType,new object[]{new Action(()=>{})});
  var siegeSetData=siegeReportType.GetMethod("SetData");
  object[] siegeArgs={"Ostican","",12,8,30,22,"Esturgia","Vlandia",15d,true,false};
  siegeSetData.Invoke(siegeReport,siegeArgs);
  string siegeDescription=(string)siegeReportType.GetProperty("Description").GetValue(siegeReport);
  Require(siegeDescription.Contains("As forças de Esturgia conquistaram") &&
    !siegeDescription.Contains("Sua companhia pessoal"),
    "Remote kingdom siege must not be credited to the player's personal party");
  siegeArgs[10]=true;
  siegeSetData.Invoke(siegeReport,siegeArgs);
  siegeDescription=(string)siegeReportType.GetProperty("Description").GetValue(siegeReport);
  Require(siegeDescription.Contains("Suas tropas conquistaram"),
    "Siege led by the player retains personal attribution");
  Console.WriteLine("Siege reports: remote kingdom victories vs direct player capture attribution passed");
  var personalWar=asm.GetType("CasusBelli.Disputes.PersonalTitleWar",true);
  var intervention=personalWar.GetMethod("AutoInterventionEligible",BindingFlags.Static|BindingFlags.NonPublic);
  Func<bool,bool,bool,bool> mayIntervene=(playerRuler,royalAuthority,externalWar)=>
    (bool)intervention.Invoke(null,new object[]{playerRuler,royalAuthority,externalWar});
  Require(!mayIntervene(false,true,false)&&!mayIntervene(true,true,true)&&
    !mayIntervene(false,false,true)&&mayIntervene(false,true,true),
    "Royal intervention only for external kingdom wars, never bandit hostilities");
  Console.WriteLine("Personal wars: royal emergency eligibility independent of bandit hostility passed");
  var crownRules=personalWar.GetMethod("RoyalInterventionRules",BindingFlags.Static|BindingFlags.NonPublic);
  Func<bool,bool,bool,bool,int,float,int,int,bool> ruler=(active,war,principal,backed,authority,influence,day,next)=>
    (bool)crownRules.Invoke(null,new object[]{active,war,principal,backed,authority,influence,day,next});
  Require(ruler(true,true,false,false,60,30,100,100)&&
    !ruler(false,true,false,false,80,200,100,0)&&
    !ruler(true,false,false,false,80,200,100,0)&&
    !ruler(true,true,true,false,80,200,100,0)&&
    !ruler(true,true,false,true,80,200,100,0)&&
    !ruler(true,true,false,false,59,200,100,0)&&
    !ruler(true,true,false,false,80,29,100,0)&&
    !ruler(true,true,false,false,80,200,100,101),
    "Crown intervention requires war, neutrality, authority, influence and cooldown");
  var autoRules=personalWar.GetMethod("AutoInterventionReady",BindingFlags.Static|BindingFlags.NonPublic);
  Func<bool,bool,bool,int,float,float,bool> auto=(isPlayerSovereign,permitted,external,days,balance,influence)=>
    (bool)autoRules.Invoke(null,new object[]{isPlayerSovereign,permitted,external,days,balance,influence});
  Require(auto(false,true,true,7,40,90) &&
    !auto(true,true,true,7,40,90) && !auto(false,false,true,7,40,90) &&
    !auto(false,true,false,7,40,90) && !auto(false,true,true,6,40,90) &&
    !auto(false,true,true,7,39,90) && !auto(false,true,true,7,40,89),
    "AI king must wait seven war days, require unrest, external kingdom war and an influence reserve");
  Console.WriteLine("Royal intervention: political neutrality, delayed AI action, crown reserve and cooldown passed");
  var majority=personalWar.GetMethod("HasPoliticalMajority",BindingFlags.Static|BindingFlags.NonPublic);
  Func<int,int,bool> major=(yes,peers)=>(bool)majority.Invoke(null,new object[]{yes,peers});
  Require(!major(0,0)&&!major(4,8)&&major(5,8)&&major(4,7)&&!major(9,8),"Absolute majority, including abstentions and invalid counts");
  var originalHolder=duchy.deJure; var oldHolderLeader=originalHolder.Clan.Leader;
  Field(originalHolder.Clan,"_leader",originalHolder);
  Field(h.Clan,"_kingdom",originalHolder.Clan.Kingdom);
  duchy.Claims[h]=ClaimType.Fabricated;
  var ducalDispute=new CasusBelli.Disputes.TitleDispute(duchy,h);
  Require(ducalDispute.IsDucal&&ducalDispute.IsActive&&ducalDispute.CanDemand,"Mature ducal claim can be tracked without a physical fief");
  var directOwners=duchy.Vassals.Select(v=>v.deJure).ToList();
  var directFiefs=duchy.Vassals.Select(v=>v.Fief==null?null:v.Fief.OwnerClan).ToList();
  var transfer=personalWar.GetMethod("TransferDucalTitle",BindingFlags.Static|BindingFlags.NonPublic);
  Require((bool)transfer.Invoke(null,new object[]{ducalDispute}),"Ducal transfer");
  Require(duchy.deJure==h&&!duchy.HeroHasValidClaim(h)&&duchy.GetHeroClaim(originalHolder)==ClaimType.PreviousOwner&&!ducalDispute.IsActive&&ducalDispute.Finished,"Title transfer and terminal dispute state");
  Require(duchy.Vassals.Select(v=>v.deJure).SequenceEqual(directOwners)&&duchy.Vassals.Select(v=>v.Fief==null?null:v.Fief.OwnerClan).SequenceEqual(directFiefs),"Subordinate titles and settlements unchanged");
  m.InheritTitle(h,originalHolder,duchy);duchy.RemoveClaim(originalHolder);
  duchy.Claims[h]=ClaimType.Fabricated;
  Require(!ducalDispute.IsActive,"Finished dispute must not revive when title and claim return");
  duchy.RemoveClaim(h);Field(h.Clan,"_kingdom",originalRealm);Field(originalHolder.Clan,"_leader",oldHolderLeader);
  Console.WriteLine("Ducal disputes: majority, mature claim, title-only transfer and former-owner claim passed");
  var stable=disputeAi.GetMethod("DucalAiStable",BindingFlags.Static|BindingFlags.NonPublic);
  Func<bool,bool,int,int,int,bool> mayAct=(war,crisis,authority,quiet,day)=>(bool)stable.Invoke(null,new object[]{war,crisis,authority,quiet,day});
  Require(mayAct(false,false,64,100,100)&&!mayAct(true,false,50,0,100)&&!mayAct(false,true,50,0,100)&&!mayAct(false,false,65,0,100)&&!mayAct(false,false,50,101,100),"Ducal initiative requires stability and respects centralization");
  var force=disputeAi.GetMethod("DucalAiSafeForce",BindingFlags.Static|BindingFlags.NonPublic);
  Func<int,int,bool> safe=(attack,defense)=>(bool)force.Invoke(null,new object[]{attack,defense});
  Require(!safe(0,0)&&!safe(149,100)&&safe(150,100)&&safe(100,0),"Ducal AI military reserve");
  var reserve=disputeAi.GetMethod("DucalAiResources",BindingFlags.Static|BindingFlags.NonPublic);
  Func<float,float,float,float,bool,bool> funds=(gold,infl,gcost,icost,fabricate)=>(bool)reserve.Invoke(null,new object[]{gold,infl,gcost,icost,fabricate});
  Require(!funds(5999,120,1000,50,true)&&!funds(6000,119,1000,50,true)&&funds(6000,120,1000,50,true)&&funds(5000,70,0,40,false),"Ducal AI economic reserves");
  var interest=disputeAi.GetMethod("DucalAiLocalInterest",BindingFlags.Static|BindingFlags.NonPublic);
  Require(!(bool)interest.Invoke(null,new object[]{0,false})&&(bool)interest.Invoke(null,new object[]{1,false})&&(bool)interest.Invoke(null,new object[]{0,true}),"Ducal AI is local or former owner");
  var cooldown=typeof(CasusBelli.TitleDisputeBehavior).GetMethod("DucalCooldownReady",BindingFlags.Static|BindingFlags.NonPublic);
  Func<int,int,int,int,bool> ready=(day,realm,clan,duke)=>(bool)cooldown.Invoke(null,new object[]{day,realm,clan,duke});
  Require(ready(168,84,168,168)&&!ready(83,84,0,0)&&!ready(167,0,168,0)&&!ready(167,0,0,168),"Separate realm, clan and title cooldowns");
  var politicsState=new CasusBelli.RealmPolitics();
  var writePolitics=typeof(CasusBelli.RealmPolitics).GetMethod("Write",BindingFlags.Instance|BindingFlags.NonPublic);
  writePolitics.Invoke(politicsState,new object[]{"ducal_title|test","168"});
  var politicsSave=new RoyalStore(){IsSaving=true};politicsState.SyncData(politicsSave);
  var restoredPolitics=new CasusBelli.RealmPolitics();politicsSave.IsSaving=false;restoredPolitics.SyncData(politicsSave);
  var number=typeof(CasusBelli.RealmPolitics).GetMethod("Number",BindingFlags.Instance|BindingFlags.NonPublic);
  Require((int)number.Invoke(restoredPolitics,new object[]{"ducal_title|test",0})==168,"Ducal cooldown persists across save/load");
  Console.WriteLine("Ducal AI: stability, territorial interest, economic reserve, superior force and persistent cooldowns passed");
  if(Environment.GetEnvironmentVariable("CB_TEST_DUCAL_ONLY")=="1")return;
  var claimantClan=Blank<Clan>();Field(claimantClan,"_kingdom",kingdoms[0]);var challenger=Blank<Hero>();Field(challenger,"_clan",claimantClan);Field(claimantClan,"_leader",challenger);
  t0.AddOngoingClaim(challenger);
  var dispute=new CasusBelli.Disputes.TitleDispute(t0,challenger);
  var invited=Blank<Clan>();Field(invited,"_kingdom",kingdoms[0]);Field(invited,"_leader",Blank<Hero>());
  if(!dispute.IsActive || !dispute.CanSend(invited,true) || !dispute.CanSend(invited,false))throw new Exception("symmetry");
  dispute.Send(invited,true);dispute.Send(invited,false);
  if(dispute.Offers.Count!=2 ||dispute.CanAnswer(dispute.Offers[0])||dispute.CanSend(invited,true))throw new Exception("travel/dispatch limits");
  foreach(var offer in dispute.Offers)Field(offer,"<Arrival>k__BackingField",CampaignTime.Now);
  if(!dispute.CanAnswer(dispute.Offers[0])||!dispute.CanAnswer(dispute.Offers[1]))throw new Exception("delivery");
  dispute.Offers[0].GetType().GetMethod("Answer",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(dispute.Offers[0],new object[]{true});
  if(dispute.CanAnswer(dispute.Offers[1]))throw new Exception("double commitment");
  Field(claimantClan,"_kingdom",kingdoms[1]);if(dispute.IsActive)throw new Exception("realm departure");
  Console.WriteLine("Internal dispute: reciprocal invitations, travel, commitment and realm departure passed");
  var rootsValue=(System.Collections.IEnumerable)hierarchyVmType.GetProperty("Roots").GetValue(hierarchyVm);
  var nodeVmType=asm.GetType("CasusBelli.UI.Titles.KingdomHierarchyNodeVM",true);
  var branchProp=nodeVmType.GetProperty("Branch");
  var stringIdProp=nodeVmType.GetProperty("StringId");
  Func<object,int> countNode=null;
  countNode=node=>{
    int total=1;
    foreach(var child in (System.Collections.IEnumerable)branchProp.GetValue(node))
      total+=countNode(child);
    return total;
  };
  int prefabNodeCount=0;object townNode=null;
  Action<object> inspectNode=null;
  inspectNode=node=>{
    prefabNodeCount++;
    if((string)stringIdProp.GetValue(node)=="town_V1")townNode=node;
    foreach(var child in (System.Collections.IEnumerable)branchProp.GetValue(node))
      inspectNode(child);
  };
  foreach(var rootNode in rootsValue)inspectNode(rootNode);
  Require(prefabNodeCount==allVlandia.Count(),
    "Prefab hierarchy VM must expose every feudal title exactly once");
  Require(townNode!=null,"Prefab hierarchy must expose county nodes");
  Require(nodeVmType.GetProperty("PortraitIdentifier")!=null &&
    nodeVmType.GetProperty("ImageIdentifier")==null,
    "Tree nodes must use lightweight 2D portrait identifiers only");
  nodeVmType.GetMethod("ExecuteSelect").Invoke(townNode,null);
  Require(((string)hierarchyVmType.GetProperty("SelectedText").GetValue(hierarchyVm))
    .Contains(m.GetTitleByStringId("town_V1").FullName.ToString()),
    "Selecting a prefab node must update hierarchy selection");
  var detailsVm=hierarchyVmType.GetProperty("Details").GetValue(hierarchyVm);
  var detailsVmType=detailsVm.GetType();
  Require((bool)detailsVmType.GetProperty("IsOpen").GetValue(detailsVm),
    "Clicking a title should open the 3D details window");
  Require(!(bool)hierarchyVmType.GetProperty("CanInteractHierarchy").GetValue(hierarchyVm),
    "Opening details must disable hierarchy input");
  Require(detailsVmType.GetProperty("Character").PropertyType.Name=="HeroViewModel",
    "Only the details window should own the 3D HeroViewModel");
  detailsVmType.GetMethod("ExecuteClose").Invoke(detailsVm,null);
  Require((bool)hierarchyVmType.GetProperty("CanInteractHierarchy").GetValue(hierarchyVm),
    "Closing details must restore hierarchy input");
  hierarchyVmType.GetMethod("ExecuteDeFacto").Invoke(hierarchyVm,null);
  Require((bool)hierarchyVmType.GetProperty("CanDeJure").GetValue(hierarchyVm),
    "De facto mode should enable return to de jure");
  hierarchyVmType.GetMethod("ExecuteDeJure").Invoke(hierarchyVm,null);

  string hierarchyPrefab=Path.Combine(Environment.CurrentDirectory,
    "GUI","Prefabs","Kingdom","CasusBelli","CBKingdomHierarchy.xml");
  string nodePrefab=Path.Combine(Environment.CurrentDirectory,
    "GUI","Prefabs","Items","CBKingdomHierarchyNode.xml");
  string detailsPrefab=Path.Combine(Environment.CurrentDirectory,
    "GUI","Prefabs","Kingdom","CasusBelli","CBKingdomHierarchyDetails.xml");
  Require(File.Exists(hierarchyPrefab)&&File.Exists(nodePrefab)&&File.Exists(detailsPrefab),
    "Hierarchy prefab files must exist");
  var hierarchyXml=new XmlDocument{XmlResolver=null};hierarchyXml.Load(hierarchyPrefab);
  var nodeXml=new XmlDocument{XmlResolver=null};nodeXml.Load(nodePrefab);
  var detailsXml=new XmlDocument{XmlResolver=null};detailsXml.Load(detailsPrefab);
  Require(hierarchyXml.SelectSingleNode("//EncyclopediaTroopScrollablePanel[@PanWithMouseEnabled='true']")!=null &&
    hierarchyXml.SelectNodes("//ScrollbarWidget").Count>=2,
    "Hierarchy prefab must provide pannable two-axis scrolling");
  Require(hierarchyXml.SelectSingleNode("//*[@Command.Click='ExecuteDeFacto']")!=null &&
    hierarchyXml.SelectSingleNode("//*[@Command.Click='ExecuteDeJure']")!=null &&
    hierarchyXml.SelectSingleNode("//*[@Command.Click='ExecutePolitics']")!=null,
    "Hierarchy prefab must own navigation and de facto/de jure controls");
  var nativeNode=nodeXml.SelectSingleNode("//EncyclopediaUnitTreeNodeItemBrushWidget");
  var lineBrushAttr=nativeNode==null?null:nativeNode.Attributes["LineBrush"];
  Require(nativeNode!=null && lineBrushAttr!=null &&
    lineBrushAttr.Value=="Encyclopedia.CharacterTree.Line" &&
    nodeXml.SelectSingleNode("//ListPanel[@Id='ChildContainer']/ItemTemplate/CBKingdomHierarchyNode")!=null,
    "Node prefab must use native recursive tree connectors");
  Require(nodeXml.SelectSingleNode("//TextWidget[@Text='@NameText'][@Brush.FontSize='16']")!=null &&
    nodeXml.SelectSingleNode("//TextWidget[@Text='@HolderText'][@Brush.FontSize='12']")!=null,
    "Node typography must live in prefab XML");
  Require(nodeXml.SelectSingleNode("//ImageIdentifierWidget[@DataSource='{PortraitIdentifier}']")!=null &&
    nodeXml.SelectNodes("//EncyclopediaCharacterTableauWidget").Count==0,
    "Hierarchy nodes must render 2D portraits and no 3D tableaus");
  Require(detailsXml.SelectNodes("//EncyclopediaCharacterTableauWidget").Count==1 &&
    detailsXml.SelectSingleNode("//*[@Command.Click='ExecuteFeudalActions']")!=null &&
    detailsXml.SelectSingleNode("//*[@Command.Click='ExecuteClaim']")!=null &&
    detailsXml.SelectSingleNode("//*[@Command.Click='ExecuteClose']")!=null,
    "Details prefab must contain one 3D character and right-side title actions");
  Require(detailsXml.SelectSingleNode("//ButtonWidget[@Id='CbTitleDetailsModalBlocker'][@Command.Click='ExecuteClose']")!=null &&
    detailsXml.SelectSingleNode("//Widget[@RenderLate='true']")!=null,
    "Details movie must contain its own modal blocker");
  Require(hierarchyXml.SelectSingleNode("//CBKingdomHierarchyDetails")==null,
    "Details must not be embedded inside the kingdom hierarchy widget tree");
  var detailsWindowType=asm.GetType("CasusBelli.UI.Titles.KingdomHierarchyDetailsWindow",true);
  Require(detailsWindowType.BaseType.Name=="GlobalLayer" &&
    (int)detailsWindowType.GetField("LayerPriority",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).GetRawConstantValue()>=1000,
    "Title details must use a high-priority GlobalLayer for modal input");

  foreach(var typeName in new[]{
    "CasusBelli.UI.Titles.KingdomHierarchyTabPrefabPatch",
    "CasusBelli.UI.Titles.KingdomHierarchyPanelPrefabPatch",
    "CasusBelli.UI.Titles.KingdomHierarchyMixin"})
    Require(asm.GetType(typeName,true)!=null,"Missing prefab integration type "+typeName);
  var closePatch=asm.GetType("CasusBelli.UI.Titles.KingdomHierarchyNativeTabClosePatch",true);
  new HarmonyLib.Harmony("cb.integration.prefab.hierarchy").CreateClassProcessor(closePatch).Patch();
  Console.WriteLine("Prefab feudal hierarchy: recursive VM, XML layout, native connectors and tab integration passed");
  Console.WriteLine("Real Harmony name getter, repeated lookup, gender, untitled hero and prefab hierarchy navigation passed");
  PoliticalChecks(campaign,kingdoms,alive,m,doc);
  Console.WriteLine("XML runtime: verified "+doc.SelectNodes("//*[@settlement]").Count+" landed titles with holders, ranks and hierarchy");
 }
}

class RoyalStore : IDataStore {
public Dictionary<string,object> Data=new Dictionary<string,object>();
public bool IsSaving{get;set;}public bool IsLoading{get{return !IsSaving;}}
public bool SyncData<T>(string key,ref T value){if(IsSaving){Data[key]=value;return true;}object obj;if(!Data.TryGetValue(key,out obj))return false;value=(T)obj;return true;}
}