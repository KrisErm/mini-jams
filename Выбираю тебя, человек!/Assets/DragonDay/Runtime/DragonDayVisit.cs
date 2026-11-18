using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DragonDay
{
    public sealed partial class DragonDayGame
    {
        [Header("Shared activities and final choice")]
        [SerializeField] private SpriteRenderer landingShadow, hiddenStar, treatPlate;
        [SerializeField] private Sprite[] offerHand, treatStages;

        private readonly int[] visitStep = new int[3];
        private readonly HashSet<int> visitedTogether = new HashSet<int>();
        private bool visitPhase, lineupReady, stashedBuckle, heldCrystal, starFound;
        private float visitAt;
        private int gemOrder;
        private Vector3 dragonScale, gemScale;
        private static readonly Vector2[] Lineup = { new Vector2(208,201), new Vector2(254,201), new Vector2(161,201) };
        private static readonly Vector2 PlatePlace = new Vector2(131,197);

        private bool CanGather => finished.Count==3 && visitPhase && visitedTogether.Count>=2 && !evening && !lineupReady;
        private bool ChaseOfferAvailable => CanGather && !chaseResolved;
        private bool MayVisit(int id) => visitPhase && finished.Contains(id) && !visitedTogether.Contains(id) && !evening;
        private bool IsAtVisitStation(int id) => id == 1 && chasing;

        private void ResetVisit()
        {
            Array.Clear(visitStep, 0, visitStep.Length); visitedTogether.Clear();
            visitAt = 0;
            visitPhase = lineupReady = stashedBuckle = heldCrystal = starFound = false;
            chasing = chaseFailedOnce = chaseResolved = false; chaseLeft = chaseRepath = chaseGrace = 0;
            chaseRoute.Clear();
            landingShadow.enabled = hiddenStar.enabled = treatPlate.enabled = false;
            treatPlate.sprite = treatStages[0];
            gem.sortingOrder = gemOrder; gem.transform.localScale = gemScale;
            dragon.transform.localScale = dragonScale;
            ResetGuestGames();
        }

        private IEnumerator Hop(Action after = null)
        {
            mode = Mode.Cut; asleep = false; hidden = false; dragon.enabled = true;
            Vector2 from = foot, dest = At(landing);
            if (!CanWalk(dest)) dest = new Vector2(226,183);
            float start = Time.time;
            landingShadow.enabled = true;
            landingShadow.transform.position = World(dest + new Vector2(0,1));
            landingShadow.sortingOrder = Depth(dest.y) - 2;
            for (float t = 0; t < 1; t = (Time.time - start) / .43f)
            {
                t = Mathf.Clamp01(t);
                Vector2 line = Vector2.Lerp(from, dest, Mathf.SmoothStep(0f, 1f, t));
                SetDragon(line);
                dragon.transform.position = World(line - new Vector2(0, 9 + 13f * Mathf.Sin(Mathf.PI * t)));
                dragon.sortingOrder = 780;
                dragon.sprite = walkSouth[t < .48f ? 1 : 3];
                float shape = 1f + (t > .78f ? .12f * Mathf.Sin((t - .78f) * Mathf.PI / .22f) : 0);
                dragon.transform.localScale = Vector3.Scale(dragonScale, new Vector3(shape, 2 - shape, 1));
                landingShadow.color = new Color(1,1,1,.20f + .75f*t);
                yield return null;
            }
            SetDragon(dest); dragon.sprite = walkSouth[0];
            dragon.transform.localScale = dragonScale;
            landingShadow.enabled = false; Play(click);
            mode = Mode.Walk;
            StartGameplayMusic();
            if(!opened&&!viewedWindow)Think(201,6f);
            after?.Invoke();
        }

        private void CarrySmall(SpriteRenderer item, bool strap)
        {
            int side = facing == 1 ? -1 : 1;
            item.transform.position = World(foot + (strap ? new Vector2(side*12,-4) : new Vector2(side*13,0)));
            item.sortingOrder = dragon.sortingOrder + (facing == 3 ? -1 : 1);
        }

        private void VisitCarry()
        {
            if (stole) { buckle.enabled = !hidden; if (!hidden) CarrySmall(buckle,true); }
            if (!heldCrystal) return;
            gem.enabled = !hidden;
            if(gem.enabled)
            {
                CarrySmall(gem,false);
                if(carryPot||stole)gem.transform.position+=new Vector3(facing==1?.68f:-.68f,.06f,0);
            }
            gem.transform.localScale = gemScale * (huntHot ? 1.26f + .14f*Mathf.Sin(Time.time*10f) : 1f);
        }

        private void CrystalActions()
        {
            if(visitStep[0]!=1){LookAtHome(1);return;}
            if(heldCrystal)return;
            Pick("Сундук дракончика",Opt("Взять камешек для поиска",()=>{
                noticedHome[1]=true;
                heldCrystal=true; CarrySmall(gem,false); Think(306,5f);
            }));
        }

        private void ReturnCrystal()
        {
            heldCrystal = false; gem.enabled = true; gem.transform.position = gemOrigin;
            gem.sortingOrder = gemOrder; gem.transform.localScale = gemScale;
        }

        private void BenchActions()
        {
            if (stole) { Pick("Пряжка Ивы", Opt("Спрятать пряжку под лавкой", StashBuckle)); return; }
            if (stashedBuckle)
            {
                Pick("Под лавкой",Opt("Забрать пряжку", () => { stashedBuckle = false; stole = true; CarrySmall(buckle,true); }));
                return;
            }
        }

        private void StashBuckle()
        {
            stole = false; stashedBuckle = true;
            buckle.transform.position = World(new Vector2(137,224));
            buckle.sortingOrder = Depth(229)-1;
            if (!hasButton)
            {
                button.enabled = true; button.transform.position = World(new Vector2(137,237));
                button.sortingOrder = Depth(236);
            }
            Say(new[]{69,233});
        }

        private bool VisitTick()
        {
            if (!visitPhase && visitAt > 0 && Time.time >= visitAt)
            {
                QuietChores();
                visitPhase=true;nextInvite=Time.time+3f;
            }
            if(!chasing&&!evening)TickGuestGames();
            return chasing && ChaseTick();
        }

        private void VisitMenu(int id,bool fromLantern=false)
        {
            if(inviting==id)ReleaseInvitation();
            var c = new List<Choice>();
            if(id==1&&!fromLantern&&ChaseOfferAvailable)
            {
                c.Add(Opt(LampTrial?"Отложить фонарики и сыграть":"Сыграть в догонялки",RequestChase));
            }
            else if (MayVisit(id))
            {
                if (id == 0)
                    c.Add(visitStep[0] == 0 ? Opt("Сыграть в поиск по карте", BeginStarHunt)
                                          : Opt("Попросить подсказку", () => Say(new[]{!heldCrystal?307:huntMark==0?285:316})));
                else if (id == 1)
                    c.Add(LampTrial?Opt("Отложить испытание",PutLampsAside):Opt("Попробовать зажечь фонарики",BeginLamps));
                else if(visitStep[2]==0)c.Add(Opt("Посадить семена",BeginSeedTrial));
                else c.Add(Opt("Продолжить посадку",OpenPlanting));
            }
            else if(visitedTogether.Contains(id))
            {
                AddPersonalMoment(c,id);
                if(c.Count==0&&id==1&&chaseResolved&&!chaseWon)
                    c.Add(Opt("Повторить догонялки",RequestChase));
                if(c.Count==0&&id==2&&!treatRewardTaken)c.Add(Opt("Посмотреть угощение",TreatMenu));
            }
            if (c.Count > 0) Pick(MageName(id),c.ToArray());
        }

        private void BeginStarHunt()
        {
            if (visitStep[0] != 0) return;
            visitStep[0] = 1;
            huntMark=0;
            MoveMark();
            Say(new[]{235,307},()=>Think(270,5f));
        }

        private void FindStar()
        {
            if(!heldCrystal||!hiddenStar.enabled||Vector2.Distance(foot,At(hiddenStar.transform))>13.5f)return;
            Play(click);
            huntMark++;
            if(huntMark==1){MoveMark();Think(308,6f);return;}
            hiddenStar.enabled=false;starFound=true;huntHot=false;
            ClearHuntThought();
            ReturnCrystal();
            Say(new[]{271},()=>FinishVisit(0));
        }

        private void TreatMenu()
        {
            if(SeedTrial){OpenPlanting();return;}
            if(!visitedTogether.Contains(2)||treatRewardTaken)return;
            var list = new List<Choice>{
                Opt("Взять ягоду", () => Taste(275,1)),
                Opt("Попробовать сухарик с мёдом", () => Taste(276,2)),
                Opt("Не брать угощение", () => Taste(277,0))
            };
            Pick("Угощение Таи", list.ToArray());
        }

        private void Taste(int line,int staged)
        {
            treatPlate.sprite = treatStages[staged];
            if (staged != 0) Play(click);
            treatRewardTaken=true;
            Say(new[]{line},()=>Think(278,3f));
        }

        private void FinishVisit(int id)
        {
            visitStep[id]=3;
            if(visitedTogether.Add(id)&&visitedTogether.Count==2)AnnounceVisitStep();
        }

        private void AnnounceVisitStep()
        {
            if(CanGather)Think(250,8f);
        }

        private void AskToGather()
        {
            Pick("Закончить визит?", Opt("Позвать всех к двери", () => StartCoroutine(LineupRoutine())));
        }

        private void ClearVisitForChoice()
        {
            hiddenStar.enabled=false; chasing=false; chaseRoute.Clear();
            ReleaseInvitation();
            ClearHuntThought();
            ReturnCrystal();
            ClearGuestGames();
        }

        private void ClearHuntThought()
        {
            if(thoughtId==270||thoughtId==306||thoughtId==308)thoughtTill=0;
        }

        private IEnumerator LineupRoutine()
        {
            if (!CanGather) yield break;
            evening = true; mode = Mode.Cut;
            QuietChores(); ClearVisitForChoice();
            yield return MoveMage(2,false,new Vector2(144,194),Lineup[2]);
            yield return MoveMage(0,false,new Vector2(186,202),Lineup[0]);
            yield return MoveMage(1,false,new Vector2(263,209),Lineup[1]);
            Say(new[]{chaseWon||chaseFailedOnce?320:109,110,251}, () => StartCoroutine(OfferHands()));
        }

        private IEnumerator OfferHands()
        {
            mode = Mode.Cut;
            for (int i=0;i<3;i++) mages[i].sprite = gestureSprites[i];
            yield return new WaitForSeconds(.21f);
            foreach (int i in new[]{2,0,1})
            {
                mages[i].sprite = offerHand[i]; yield return new WaitForSeconds(.13f);
            }
            lineupReady = true; mode = Mode.Walk; Think(252,9f);
        }

        private void ChooseHand(int id)
        {
            if (!lineupReady) return;
            Action confirm = () => Pick("Тот самый выбор", Opt("Положить лапу в ладонь", () => Finish(id)));
            int request=id==1&&!chaseWon&&!chaseFailedOnce?287:114+id;
            Say(new[]{request},confirm);
        }

        private IEnumerator SealChoice(int id)
        {
            mode = Mode.Cut; asleep = false; hidden = false; dragon.enabled = true;
            for (int i=0;i<3;i++) if (i != id) mages[i].sprite = IdleMage(i);
            Vector2 target = At(mages[id].transform) + new Vector2(2.5f,24);
            for(int i=0;i<GridCount;i++)chaseGrid[i]=CanCrossFloor(ChasePoint(i));
            PlanChase(foot,target);
            var steps=new List<Vector2>(chaseRoute);
            foreach(Vector2 stepPoint in steps)
            {
                while(Vector2.Distance(foot,stepPoint)>.4f)
                {
                    Vector2 diff=stepPoint-foot;
                    facing=Mathf.Abs(diff.x)>Mathf.Abs(diff.y)?(diff.x<0?1:2):(diff.y<0?3:0);
                    SetDragon(Vector2.MoveTowards(foot,stepPoint,56f*Time.deltaTime));
                    dragon.sprite=WalkFrames()[(int)(Time.time*11f)%4];
                    yield return null;
                }
            }
            SetDragon(target); facing=3; dragon.sprite=idleNorth[0]; mages[id].sprite=offerHand[id];
            if (id==0 && starFound)
            {
                hiddenStar.enabled=true; hiddenStar.color=Color.white;
                hiddenStar.transform.position=World(target+new Vector2(12,-2));hiddenStar.sortingOrder=Depth(target.y)+1;
            }
            yield return new WaitForSeconds(.30f);
            dragon.sprite=pledgePaw;
            Burst(World(target+new Vector2(4,-16)),id,2.55f,true);
            Play(vowChime);
            yield return new WaitForSeconds(2.48f);
            int response=id==0&&!starFound?288:id==1&&!chaseWon&&!chaseFailedOnce?286:280+id;
            Say(new[]{response,298+id,283,301},()=>End((EndingKind)id));
        }

        private void DrawVisitUi(float fit)
        {
            if (chasing)
            {
                GUI.DrawTexture(new Rect(305,12,222,66),panel);
                string clock="00:"+Mathf.Max(0,Mathf.CeilToInt(chaseLeft)).ToString("00");
                GUI.Label(new Rect(311,17,210,18),"Ускользни от Ивы",control);
                GUI.Label(new Rect(311,34,210,33),clock,timerDigits);
            }
            if (lineupReady && chosen == -1)
                for (int i=0;i<3;i++)
                {
                    Vector2 p=UiPoint(mages[i].transform.position,fit);
                    GUI.DrawTexture(new Rect(p.x-35,p.y-58,70,24),panel);
                    GUI.Label(new Rect(p.x-35,p.y-58,70,24),MageName(i),control);
                }
        }

        private const int GridW=55, GridH=27, GridCount=GridW*GridH;
        private readonly bool[] chaseGrid = new bool[GridCount];
        private readonly int[] chaseBack = new int[GridCount], chaseBuffer = new int[GridCount];
        private readonly List<Vector2> chaseRoute = new List<Vector2>();
        private bool chasing, chaseFailedOnce, chaseResolved;
        private float chaseLeft, chaseRepath, chaseGrace;

        private void RequestChase()
        {
            if(!CanGather||chaseWon||chasing)return;
            if(foot.y<163f){Think(325,5f);return;}
            if(LampTrial)PutLampsAside();
            ReleaseInvitation();
            Say(chaseFailedOnce?new[]{291}:new[]{240}, () => StartCoroutine(ReadyForChase()));
        }

        private IEnumerator ReadyForChase()
        {
            mode=Mode.Cut; QuietChores();
            for(int i=0;i<GridCount;i++)chaseGrid[i]=CanCrossFloor(ChasePoint(i));
            PlanChase(MageFoot(1),new Vector2(276,225));
            var approach=new List<Vector2>(chaseRoute);
            foreach(var point in approach)yield return MoveMage(1,false,point-new Vector2(0,12));
            chasing=true;hidden=false;dragon.enabled=true;chaseLeft=8f;chaseRepath=0;chaseGrace=chaseFailedOnce?1.1f:.85f;
            for(int i=0;i<GridCount;i++)chaseGrid[i]=CanCrossFloor(ChasePoint(i));
            mode=Mode.Walk;Think(242,4f);
        }

        private bool ChaseTick()
        {
            float dt=Mathf.Min(Time.deltaTime,.065f);
            chaseLeft-=Time.deltaTime;
            if(chaseLeft<=0)
            {
                chasing=false;chaseRoute.Clear();hidden=false;dragon.enabled=true;
                Say(chaseFailedOnce?new[]{321}:new[]{241,272}, () => StartCoroutine(AfterChase(true)));
                return true;
            }
            chaseGrace-=dt;chaseRepath-=dt;
            Vector2 p=At(mages[1].transform)+new Vector2(0,12);
            if (chaseRepath<=0)
            {
                PlanChase(p,foot);chaseRepath=.24f;
            }
            while (chaseRoute.Count>0 && Vector2.Distance(p,chaseRoute[0])<.7f) chaseRoute.RemoveAt(0);
            Vector2 step=chaseRoute.Count>0?chaseRoute[0]:foot;
            Vector2 next=Vector2.MoveTowards(p,step,47f*dt);
            if(CanCrossFloor(next))
            {
                SetMage(1,next-new Vector2(0,12));p=next;
                mages[1].sprite=mageWalkB[(int)(chaseLeft*8)%2];
            }
            else chaseRepath=0;
            if(chaseGrace<=0 && Vector2.Distance(p,foot)<8.5f)
            {
                chasing=false;chaseFailedOnce=true;chaseRoute.Clear();
                StartCoroutine(CaughtInChase());return true;
            }
            return false;
        }

        private IEnumerator CaughtInChase()
        {
            mode=Mode.Cut; mages[1].sprite=offerHand[1];dragon.sprite=walkSouth[0];
            dragon.transform.localScale=Vector3.Scale(dragonScale,new Vector3(1.11f,.85f,1));
            Play(click);yield return new WaitForSeconds(.64f);
            dragon.transform.localScale=dragonScale; mages[1].sprite=IdleMage(1);
            Say(new[]{289,290},()=>StartCoroutine(AfterChase(false)));
        }

        private IEnumerator AfterChase(bool passed)
        {
            mode=Mode.Cut;
            // Retrace the same walkable grid around the furniture.
            Vector2 start=At(mages[1].transform)+new Vector2(0,12), end=At(indoorSpots[1])+new Vector2(0,12);
            PlanChase(start,end);
            var steps=new List<Vector2>(chaseRoute);
            foreach(var step in steps) yield return MoveMage(1,false,step-new Vector2(0,12));
            if(ClearChaseLeg(At(mages[1].transform)+new Vector2(0,12),end))
                yield return MoveMage(1,false,At(indoorSpots[1]));
            mode=Mode.Walk;
            if(passed)chaseWon=true;
            chaseResolved=true;
            AnnounceVisitStep();
        }

        private static Vector2 ChasePoint(int id) => new Vector2(100 + (id % GridW)*4, 164 + (id / GridW)*4);

        private bool ClearChaseLeg(Vector2 a,Vector2 b)
        {
            int tests=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/2f));
            for(int j=1;j<=tests;j++) if(!CanNavigate(Vector2.Lerp(a,b,j/(float)tests)))return false;
            return true;
        }

        private int NearestChaseNode(Vector2 at)
        {
            float best=235f;int result=-1;
            for(int i=0;i<GridCount;i++)
            {
                if(!chaseGrid[i])continue;
                Vector2 p=ChasePoint(i);
                float d=(at-p).sqrMagnitude;
                if(d<best && ClearChaseLeg(at,p)) {best=d;result=i;}
            }
            return result;
        }

        private void PlanChase(Vector2 start,Vector2 target)
        {
            chaseRoute.Clear();
            if(ClearChaseLeg(start,target)) {chaseRoute.Add(target);return;}
            int from=NearestChaseNode(start),goal=NearestChaseNode(target);
            if(from<0||goal<0)return;
            for(int i=0;i<GridCount;i++)chaseBack[i]=-1;
            int tail=1;
            chaseBuffer[0]=from;chaseBack[from]=from;
            for(int head=0;head<tail&&chaseBack[goal]<0;head++)
            {
                int k=chaseBuffer[head];
                int x=k%GridW,y=k/GridW;
                if(x>0) ChaseEdge(k,k-1,ref tail);
                if(x<GridW-1) ChaseEdge(k,k+1,ref tail);
                if(y>0) ChaseEdge(k,k-GridW,ref tail);
                if(y<GridH-1) ChaseEdge(k,k+GridW,ref tail);
            }
            if(chaseBack[goal]<0)return;
            for(int n=goal;;n=chaseBack[n])
            {
                chaseRoute.Add(ChasePoint(n));
                if(n==from)break;
            }
            chaseRoute.Reverse();
            if(ClearChaseLeg(ChasePoint(goal),target))chaseRoute.Add(target);
        }

        private void ChaseEdge(int from,int to,ref int tail)
        {
            if(!chaseGrid[to]||chaseBack[to]>=0||!ClearChaseLeg(ChasePoint(from),ChasePoint(to)))return;
            chaseBack[to]=from;chaseBuffer[tail++]=to;
        }
    }
}
