using System;
using System.Collections;
using UnityEngine;

namespace DragonDay
{
    public sealed partial class DragonDayGame
    {
        [Header("Guest activities")]
        [SerializeField] private SpriteRenderer[] trialLanterns;
        [SerializeField] private Sprite paperMark, paperStar, seedTray;
        [SerializeField] private BoxCollider2D lanternFootprint;
        [SerializeField] private Texture2D seedPot;
        [SerializeField] private Texture2D[] seedTokens, seedSprouts;

        private int huntMark, lampCount, plantedSeeds, selectedSeed=-1, lanternOrder;
        private float lampTime;
        private bool huntHot, chaseWon, treatRewardTaken;
        private readonly bool[] lampLit=new bool[3], noticedHome=new bool[3];
        private static readonly Vector2 HearthLook=new Vector2(281,181);
        private static readonly Vector2 ChestLook=new Vector2(261,250);
        private static readonly Vector2[] TrialPoints={
            new Vector2(220,176),new Vector2(270,208),new Vector2(136,207)
        };
        private static readonly Vector2[] Directions={
            new Vector2(0,1),new Vector2(-1,0),new Vector2(1,0),new Vector2(0,-1)
        };
        private static readonly int[] HomeLines={303,304,305}, PotOrder={2,0,1};
        private static readonly string[] SeedNames={"Лист","Ягода","Цветок"};
        private readonly bool[] seedInPot=new bool[3];
        private string seedFeedback="";
        private bool LampTrial=>visitStep[1]==1;
        private bool SeedTrial=>visitStep[2]==1;

        private void ResetGuestGames()
        {
            huntMark=lampCount=plantedSeeds=0;selectedSeed=-1;seedFeedback="";
            Array.Clear(seedInPot,0,seedInPot.Length);
            huntHot=chaseWon=treatRewardTaken=false;
            Array.Clear(noticedHome,0,noticedHome.Length);
            hiddenStar.sprite=paperStar;
            SetTrialLanterns(false);
            cup.enabled=false;
        }

        private bool CanDescribeHome(int thing)
            => !noticedHome[thing] && (thing!=1 || visitStep[0]!=1);

        private void LookAtHome(int thing)
        {
            if(!CanDescribeHome(thing))return;
            noticedHome[thing]=true;
            Think(HomeLines[thing],4.5f);
        }

        private void DismissHomeLook()
        {
            if(Array.IndexOf(HomeLines,thoughtId)>=0)thoughtTill=0;
        }

        private void ObserveHome()
        {
            if(asleep||evening||chasing||Time.unscaledTime<thoughtTill)return;
            if(CanDescribeHome(0)&&(foot-HearthLook).sqrMagnitude<=16f*16f)
                LookAtHome(0);
            else if(CanDescribeHome(1)&&(foot-ChestLook).sqrMagnitude<=19f*19f)
                LookAtHome(1);
            else if(cup.enabled&&CanDescribeHome(2)&&(foot-At(cup.transform)).sqrMagnitude<=21f*21f)
                LookAtHome(2);
        }

        private Vector2 MarkSpot(int index)
        {
            if(index==0)return new Vector2(137,246);
            return mapBurned?new Vector2(247,220):new Vector2(235,215);
        }

        private void MoveMark()
        {
            Vector2 p=MarkSpot(huntMark);
            hiddenStar.transform.position=World(p);
            hiddenStar.sortingOrder=Depth(p.y);
            hiddenStar.sprite=huntMark==0?paperMark:paperStar;
            hiddenStar.enabled=false;
            huntHot=false;
        }

        private void TickGuestGames()
        {
            if(visitStep[0]==1)
            {
                bool warm=heldCrystal&&Vector2.Distance(foot,At(hiddenStar.transform))<51f;
                hiddenStar.enabled=warm;
                huntHot=warm;
                if(warm)
                    hiddenStar.color=new Color(1f,1f,.79f,.7f+.3f*Mathf.Sin(Time.time*6f)*Mathf.Sin(Time.time*6f));
            }
            if(!LampTrial||lampCount==0)return;
            lampTime-=Time.deltaTime;
            if(lampTime>0)return;
            SetTrialLanterns(true);
            Think(310,5f);
        }

        private void SetTrialLanterns(bool visible)
        {
            lampTime=0;lampCount=0;
            for(int i=0;i<trialLanterns.Length;i++)
            {
                SpriteRenderer obj=trialLanterns[i];
                bool original=obj==lantern;
                lampLit[i]=false;
                obj.sprite=original&&!visible&&lit?litLantern:firstLantern;
                obj.transform.position=original&&!visible?lanternOrigin:World(TrialPoints[i]);
                obj.sortingOrder=original&&!visible?lanternOrder:Depth(TrialPoints[i].y+5);
                obj.enabled=visible||original&&arrived;
            }
            lanternFootprint.enabled=!visible&&arrived;
        }

        private void BeginLamps()
        {
            if(visitStep[1]!=0)return;
            visitStep[1]=1;
            Say(new[]{309,318},()=>SetTrialLanterns(true));
        }

        private void PutLampsAside()
        {
            visitStep[1]=0;
            SetTrialLanterns(false);
        }

        private bool InFireCone(Vector2 delta)
        {
            return delta.sqrMagnitude>=5f*5f&&delta.sqrMagnitude<=40f*40f
                &&Vector2.Dot(Directions[facing],delta.normalized)>.76f;
        }

        private void IgniteTrialLamp()
        {
            int next=-1;float nearest=40f*40f+1f;
            for(int i=0;i<TrialPoints.Length;i++)
            {
                Vector2 delta=TrialPoints[i]-foot;
                if(!lampLit[i]&&delta.sqrMagnitude<nearest&&InFireCone(delta)&&ClearChaseLeg(foot,TrialPoints[i]))
                {next=i;nearest=delta.sqrMagnitude;}
            }
            if(next<0)return;
            if(lampCount==0)lampTime=10f;
            lampLit[next]=true;lampCount++;
            trialLanterns[next].sprite=litLantern;
            Burst(trialLanterns[next].transform.position,1,.8f,false);
            if(lampCount!=trialLanterns.Length)return;
            Say(new[]{311},()=>{FinishVisit(1);SetTrialLanterns(false);});
        }

        private void DrawLampTrial(float fit)
        {
            if(!LampTrial||chasing)return;
            float x=(832f+Screen.width/fit)*.5f-246f;
            GUI.DrawTexture(new Rect(x,12,232,66),panel);
            GUI.Label(new Rect(x+5,17,222,19),"Фонарики  "+lampCount+"/3",control);
            if(lampCount==0)GUI.Label(new Rect(x+5,40,222,22),"Отсчёт с первого огонька",control);
            else GUI.Label(new Rect(x+5,32,222,40),"00:"+Mathf.CeilToInt(Mathf.Max(0,lampTime)).ToString("00"),timerDigits);
        }

        private void BeginSeedTrial()
        {
            if(visitStep[2]!=0)return;
            visitStep[2]=1;
            StartCoroutine(SetTayaTray());
        }

        private IEnumerator SetTayaTray()
        {
            mode=Mode.Cut;
            for(int i=0;i<GridCount;i++)chaseGrid[i]=CanCrossFloor(ChasePoint(i));
            PlanChase(MageFoot(2),new Vector2(125,195));
            var path=new System.Collections.Generic.List<Vector2>(chaseRoute);
            foreach(var p in path)yield return MoveMage(2,false,p-new Vector2(0,12));
            mages[2].sprite=gestureSprites[2];
            treatPlate.transform.position=World(PlatePlace);
            treatPlate.sortingOrder=Depth(PlatePlace.y)-2;
            treatPlate.sprite=seedTray;treatPlate.enabled=true;
            yield return new WaitForSeconds(.3f);
            Say(new[]{245},()=>{mages[2].sprite=IdleMage(2);OpenPlanting();});
        }

        private void OpenPlanting()
        {
            if(SeedTrial)mode=Mode.Planting;
        }

        private void PlantSeed(int potIndex)
        {
            if(!SeedTrial||seedInPot[potIndex])return;
            if(selectedSeed<0){seedFeedback="Сначала выбери семечко в верхнем ряду.";return;}
            if(selectedSeed!=potIndex)
            {
                seedFeedback="Чуть не перепутали. Найди на горшочке такой же значок.";
                return;
            }
            seedInPot[potIndex]=true;plantedSeeds++;selectedSeed=-1;
            Play(click);
            seedFeedback=plantedSeeds==3?"Все семена на месте.":"Посажено! Выбери следующее семечко.";
        }

        private void DrawPlanting(float fit)
        {
            GUI.DrawTexture(new Rect(416-Screen.width/fit*.5f,0,Screen.width/fit,640),tint);
            DrawWoodWindow(new Rect(180,137,472,366));
            GUI.Label(new Rect(200,152,432,26),"Посадка с Таей   "+plantedSeeds+"/3",speaker);
            GUI.Label(new Rect(200,182,432,37),"Выбери семечко, затем горшочек с таким же знаком.",taskText);
            for(int i=0;i<3;i++)
            {
                float x=213+139*i;
                if(!seedInPot[i])
                {
                    if(B(x,222,128,selectedSeed==i?"✓  "+SeedNames[i]:SeedNames[i]))
                    {selectedSeed=i;seedFeedback="Теперь выбери подходящий горшочек.";}
                    GUI.DrawTexture(new Rect(x+50,270,28,28),seedTokens[i]);
                }
                int pot=PotOrder[i];
                if(GUI.Button(new Rect(x+20,307,88,76),GUIContent.none,option))PlantSeed(pot);
                GUI.DrawTexture(new Rect(x+38,313,52,52),seedInPot[pot]?seedSprouts[pot]:seedPot);
                if(!seedInPot[pot])GUI.DrawTexture(new Rect(x+54,343,20,20),seedTokens[pot]);
            }
            GUI.Label(new Rect(201,390,430,42),seedFeedback,control);
            if(plantedSeeds==3)
            {
                if(B(291,443,250,"Готово"))
                {
                    treatPlate.sprite=treatStages[0];
                    Say(new[]{315},()=>FinishVisit(2));
                }
            }
            else if(B(291,443,250,"Вернуться в комнату"))mode=Mode.Walk;
        }

        private void RestoreCup()
        {
            steamUntil=0;
            cup.transform.position=cupOrigin;
            cup.sortingOrder=cupOrder;
        }

        private void ClearGuestGames()
        {
            if(LampTrial)visitStep[1]=0;
            if(SeedTrial){visitStep[2]=0;treatPlate.enabled=false;}
            if(visitStep[0]==1)visitStep[0]=0;
            SetTrialLanterns(false);
            RestoreCup();
        }
    }
}
