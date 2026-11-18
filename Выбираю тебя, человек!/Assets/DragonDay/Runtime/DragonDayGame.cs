using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DragonDay
{
    public sealed partial class DragonDayGame : MonoBehaviour
    {
        [Serializable] public sealed class Target { public string id; public Transform point; }
        [Serializable] public sealed class Line { public int id; public string speaker; public string text; public string anchor; }
        [Serializable] public sealed class Story { public Line[] lines; }
        private sealed class Choice { public string text; public Action run; public Choice(string t, Action r) { text=t; run=r; } }

        [Header("Editable scene references")]
        [SerializeField] private SpriteRenderer dragon, door, breath, map, lantern, buckle, pot, gem, cup, button, berry;
        [SerializeField] private GameObject introBackdrop;
        [SerializeField] private SpriteRenderer gloveProp, herbBasket;
        [SerializeField] private Sprite[] gestureSprites;
        [SerializeField] private SpriteRenderer[] mages;
        [SerializeField] private Transform[] yardSpots, indoorSpots;
        [SerializeField] private Transform perch, landing, threshold, porch;
        [SerializeField] private BoxCollider2D[] blockers;
        [SerializeField] private BoxCollider2D[] yardBlockers;
        [SerializeField] private BoxCollider2D[] tableBlocks;
        [SerializeField] private SpriteRenderer[] tableVisuals;
        [SerializeField] private SpriteRenderer[] ambience;
        [SerializeField] private Target[] targets;
        [SerializeField] private Camera gameCamera;
        [Header("Sprite animation frames")]
        [SerializeField] private Sprite[] walkSouth, walkWest, walkEast, walkNorth, sleepFrames, fireWest, fireEast;
        [SerializeField] private Sprite[] breathWest, breathEast, doorFrames, mageWalkA, mageWalkB, mageWalkC;
        [SerializeField] private Sprite burnedMap, litLantern, healthyPot, ivaKnockSprite;
        [Header("Story and menu")]
        [SerializeField] private TextAsset story;
        [SerializeField] private Font bodyFont, titleFont;
        [SerializeField] private Texture2D buttonNormal, buttonHover, buttonPressed;
        [SerializeField] private AudioClip knock, puff, click;

        private enum Mode { Title, Walk, Lines, Choice, Cut, End, Pause, Planting }
        private Mode mode=Mode.Title;
        private readonly Dictionary<int,Line> script=new Dictionary<int,Line>();
        private readonly HashSet<int> met=new HashSet<int>();
        private readonly HashSet<int> finished=new HashSet<int>();
        private readonly HashSet<int> heard=new HashSet<int>();
        private readonly List<Choice> options=new List<Choice>();
        private readonly bool[] hasIntro=new bool[3];
        private int[] queue=Array.Empty<int>();
        private int current, facing=0, chosen=-1;
        private Action afterLines;
        private string question="";
        private bool opened, arrived, viewedWindow, evening, asleep=true, hidden, mapBurned, mapped,
            stole, glove, lit, hasButton, mint, planted, carryPot, ate;
        private float fireUntil, potReady, step;
        private Vector2 foot;
        private AudioSource audioSource;
        private Sprite firstMap,firstLantern,firstPot;
        private Vector3 mapOrigin, lanternOrigin, potOrigin, cupOrigin, buckleOrigin, gemOrigin;
        private GUIStyle body, title, speaker, control, option, speechBody, thoughtTitle;
        private Texture2D panel, tint, thoughtPanel;
        private float previousAspect, porchRemarkAt, controlsHintUntil;
        private const string ControlsHint="WASD / стрелки — ходьба   ·   E — взаимодействие   ·   F — пыхнуть";
        private int mapOrder, buckleOrder, cupOrder, potOrder;
        private float thoughtTill, banterAt, choreClock;
        private int thoughtId, banterOwner=-1, working=-1;
        private Quaternion workRotation;
        private bool wishesPending;
        private static readonly int[] GuestIntroductions={28,29,27};

        private void Awake()
        {
            if (story!=null)
                foreach (Line l in JsonUtility.FromJson<Story>(story.text).lines) script[l.id]=l;
            audioSource=gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake=false;
            InitializeMusic();
            firstMap=map.sprite; firstLantern=lantern.sprite; firstPot=pot.sprite;
            mapOrigin=map.transform.position; lanternOrigin=lantern.transform.position;
            potOrigin=pot.transform.position; cupOrigin=cup.transform.position;
            buckleOrigin=buckle.transform.position; gemOrigin=gem.transform.position;
            mapOrder=map.sortingOrder; buckleOrder=buckle.sortingOrder; cupOrder=cup.sortingOrder; potOrder=pot.sortingOrder;lanternOrder=lantern.sortingOrder;
            gemOrder=gem.sortingOrder; gemScale=gem.transform.localScale; dragonScale=dragon.transform.localScale;
            InitializePolish();
            ResetGame();
        }

        private void ResetGame()
        {
            StopAllCoroutines(); StopGameplayMusic(); QuietChores(); introBackdrop.SetActive(false);
            mode=Mode.Title; met.Clear(); finished.Clear(); heard.Clear(); options.Clear();
            Array.Clear(hasIntro,0,hasIntro.Length);
            opened=arrived=viewedWindow=evening=hidden=mapBurned=mapped=stole=glove=lit=hasButton=mint=planted=carryPot=ate=false;
            asleep=true; chosen=-1; facing=0; fireUntil=potReady=step=0;porchRemarkAt=0;
            controlsHintUntil=0;
            ResetVisit();
            ResetInvitations();
            thoughtTill=banterAt=choreClock=0; thoughtId=0; banterOwner=-1; wishesPending=false;
            endingKind=EndingKind.Home; afterLines=null; SetDragon(At(perch)); dragon.sprite=sleepFrames[0]; dragon.enabled=true;
            door.sprite=doorFrames[0]; breath.enabled=button.enabled=berry.enabled=false;
            pot.enabled=gem.enabled=true;
            map.sprite=firstMap; map.transform.position=mapOrigin;
            lantern.sprite=firstLantern; lantern.transform.position=lanternOrigin;
            pot.sprite=firstPot; pot.transform.position=potOrigin;
            cup.transform.position=cupOrigin; buckle.transform.position=buckleOrigin; gem.transform.position=gemOrigin;
            map.sortingOrder=mapOrder; cup.sortingOrder=cupOrder; buckle.sortingOrder=buckleOrder; pot.sortingOrder=potOrder;
            ResetPolish();
            foreach(var r in ambience) r.color=Color.white;
            for(int i=0;i<mages.Length;i++){ mages[i].enabled=true; SetMage(i,At(yardSpots[i])); mages[i].sprite=IdleMage(i); }
        }

        private void StartStory()
        {
            ResetGame();
            StartCoroutine(Prologue());
        }
        private IEnumerator Prologue()
        {
            introBackdrop.SetActive(true);
            mode=Mode.Cut;
            Vector2[] stops={new Vector2(73,157),new Vector2(103,165),new Vector2(128,152)};
            float time=Time.time;
            for(float t=0;t<1;t=(Time.time-time)/1.4f)
            {
                for(int i=0;i<3;i++)
                {
                    SetMage(i,Vector2.Lerp(new Vector2(-48-i*11,186+i*6),stops[i],t));
                    mages[i].sprite=MageFrames(i)[(int)(t*14)%2];
                }
                yield return null;
            }
            for(int i=0;i<3;i++) { SetMage(i,stops[i]); mages[i].sprite=IdleMage(i); }
            Say(new[]{7,8,9,10,11,12,13,14},()=>StartCoroutine(ApproachHouse()));
        }
        private IEnumerator ApproachHouse()
        {
            introBackdrop.SetActive(false);
            mode=Mode.Cut;
            for(int i=0;i<3;i++) SetMage(i,new Vector2(263+3*i,-15-i*13));
            float start=Time.time;
            for(float t=0;t<1;t=(Time.time-start)/1.85f)
            {
                for(int i=0;i<3;i++)
                {
                    Vector2 a=new Vector2(263+3*i,-15-i*13), b=new Vector2(247+i*3,60-i*6), c=At(yardSpots[i]);
                    Vector2 p=t<.55f ? Vector2.Lerp(a,b,t/.55f) : Vector2.Lerp(b,c,(t-.55f)/.45f);
                    SetMage(i,p); mages[i].sprite=MageFrames(i)[(int)(t*14)%2];
                }
                SleepOrFire(false); yield return null;
            }
            for(int i=0;i<3;i++){ SetMage(i,At(yardSpots[i])); mages[i].sprite=IdleMage(i); }
            yield return WipeAtMat();
        }
        private IEnumerator WipeAtMat()
        {
            Vector2 mat=At(porch);
            yield return MoveMage(1,false,mat+new Vector2(14,-2),mat+new Vector2(3,0),mat);
            for(int i=0;i<4;i++)
            {
                SetMage(1,mat+new Vector2(i%2==0?-1.2f:1.2f,i%2));
                mages[1].sprite=mageWalkB[i%2];
                yield return new WaitForSeconds(.11f);
            }
            SetMage(1,mat);mages[1].sprite=IdleMage(1);
            Say(new[]{21},()=>StartCoroutine(KnockAndStepBack()));
        }
        private IEnumerator KnockAndStepBack()
        {
            mode=Mode.Cut;
            Vector2 contact=At(porch)+new Vector2(4,6);
            yield return MoveMage(1,false,contact);
            for(int tap=0;tap<3;tap++)
            {
                SetMage(1,contact+new Vector2(0,1));
                mages[1].sprite=ivaKnockSprite;
                Play(knock);
                yield return new WaitForSeconds(.095f);
                SetMage(1,contact);
                mages[1].sprite=IdleMage(1);
                yield return new WaitForSeconds(tap==2?.20f:.105f);
            }
            yield return MoveMage(1,false,At(porch)+new Vector2(12,-1),At(yardSpots[1]));
            SetMage(1,At(yardSpots[1]));
            mode=Mode.Walk;
            controlsHintUntil=Time.unscaledTime+4f;
        }
        private void Play(AudioClip c) { if(c!=null) audioSource.PlayOneShot(c,.45f); }
        private static Vector2 At(Transform t) { return new Vector2(208f+t.position.x*32f,160f-t.position.y*32f); }
        private static Vector3 World(Vector2 p) { return new Vector3((p.x-208f)/32f,(160f-p.y)/32f,0); }
        private static int Depth(float y) { return Mathf.RoundToInt(100f+y*2f); }
        private void SetDragon(Vector2 feet)
        {
            foot=feet; dragon.transform.position=World(foot-new Vector2(0,9));
            dragon.sortingOrder=asleep?780:Depth(foot.y);
            if(!asleep)
            {
                Vector3 p=World(foot);
                for(int i=0;i<tableBlocks.Length;i++)
                {
                    Bounds box=tableBlocks[i].bounds;
                    float side=p.x<box.min.x?box.min.x-p.x:p.x>box.max.x?p.x-box.max.x:0f;
                    if(side>0f && side<.43f && p.y<=box.max.y+.075f && p.y>=box.min.y-.09f)
                        dragon.sortingOrder=Mathf.Max(dragon.sortingOrder,tableVisuals[i].sortingOrder+3);
                }
            }
            breath.sortingOrder=dragon.sortingOrder+4;
        }
        private void SetMage(int i,Vector2 centre)
        {
            Vector3 p=World(centre);
            mages[i].transform.position=p;
            mages[i].sortingOrder=introBackdrop.activeSelf?1002+i:Depth(centre.y+12);
            UpdateArrivalItem(i);
        }
        private Sprite IdleMage(int i) { return MageFrames(i)[0]; }
        private Sprite[] MageFrames(int i) { return i==0?mageWalkA:i==1?mageWalkB:mageWalkC; }
        private Sprite[] WalkFrames() { return facing==0?walkSouth:facing==1?walkWest:facing==2?walkEast:walkNorth; }

        private void Update()
        {
            TickMusic();
            if(mode==Mode.Walk && arrived && !evening && !chasing)
            {
                if(TickInvitation())QuietChores();else ChoresUpdate();
            }
            else QuietChores();
            if(evening && mode!=Mode.Pause)
                foreach(var r in ambience) r.color=Color.Lerp(r.color,new Color(.92f,.82f,.76f),2.1f*Time.deltaTime);
            if(gameCamera!=null)
            {
                float aspect=(float)Screen.width/Mathf.Max(Screen.height,1);
                if(Mathf.Abs(aspect-previousAspect)>.001f)
                {
                    previousAspect=aspect;
                    gameCamera.rect=new Rect(0,0,1,1);
                    gameCamera.orthographicSize=Mathf.Max(5.0625f,12.85f/(2f*aspect));
                }
            }
            if(mode==Mode.Lines)
            {
                if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.E)) NextLine();
                SleepOrFire(false);
                return;
            }
            if(mode==Mode.Choice)
            {
                if(Input.GetKeyDown(KeyCode.Escape)) mode=Mode.Walk;
                SleepOrFire(false); return;
            }
            if(mode==Mode.Planting)
            {
                if(Input.GetKeyDown(KeyCode.Escape))mode=Mode.Walk;
                SleepOrFire(false);return;
            }
            if(mode==Mode.Pause)
            {
                if(Input.GetKeyDown(KeyCode.Escape)) mode=Mode.Walk;
                return;
            }
            if(mode==Mode.Title||mode==Mode.End) { SleepOrFire(false); return; }
            if(mode==Mode.Cut) return;
            if(wishesPending)
            {
                wishesPending=false;
                thoughtTill=0;
                Say(new[]{203});
                return;
            }
            if(VisitTick()) return;
            if(Input.GetKeyDown(KeyCode.Escape)) { mode=Mode.Pause; return; }
            if(!chasing&&Input.GetKeyDown(KeyCode.E)) { QuietChores(); Interact(); return; }
            if(Input.GetKeyDown(KeyCode.F))
            { QuietChores(); if(!asleep) FireAction(); return; }
            float x=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)
                   -(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            float y=(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0)
                   -(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0);
            var move=new Vector2(x,y);
            bool moving=move.sqrMagnitude>.01f;
            if(moving)
            {
                if(hidden) { hidden=false; dragon.enabled=true; }
                if(asleep) { StartCoroutine(Hop());return; }
                Vector2 before=foot;
                facing=Mathf.Abs(x)>=Mathf.Abs(y)&&x!=0 ? (x<0?1:2) : (y>0?0:3);
                move=move.normalized*69f*Mathf.Min(Time.deltaTime,.042f);
                Vector2 px=foot+new Vector2(move.x,0);
                if(CanWalk(px)) SetDragon(px);
                Vector2 py=foot+new Vector2(0,move.y);
                if(CanWalk(py)) SetDragon(py);
                if(arrived&&opened&&!evening&&before.y>126f&&foot.y<=126f&&Time.time>=porchRemarkAt)
                {
                    porchRemarkAt=Time.time+8f;
                    Think(322,5f);
                }
                moving=(before-foot).sqrMagnitude>.001f;
            }
            if(carryPot) Carry(pot);
            VisitCarry();
            ObserveHome();
            if(planted&&potReady>0&&Time.time>=potReady)
            {
                potReady=0; pot.sprite=healthyPot;
                Vector2 point=At(pot.transform);
                if(Vector2.Distance(point,foot)<37 && !heard.Contains(2)) ShareInterest(2);
            }
            SleepOrFire(moving);
        }

        private bool CanWalk(Vector2 p)
        {
            if(!CanCrossFloor(p))return false;
            for(int i=0;i<mages.Length;i++)
                if(mages[i].enabled)
                {
                    Vector2 other=MageFoot(i);
                    float next=ActorGap(p,other);
                    if(next<1f&&next<=ActorGap(foot,other))return false;
                }
            return true;
        }
        private bool CanCrossFloor(Vector2 p)
        {
            bool inRoom=p.x>=99 && p.x<=317 && p.y>=163 && p.y<=268;
            // Limit the crossing through the wall, not movement on the interior floor.
            bool passage=opened && !chasing && p.x>=204 && p.x<=213 && p.y>=109 && p.y<163;
            bool yard=opened && !chasing && p.x>=78 && p.x<=340 && p.y>=23 && p.y<=126;
            if(!(inRoom||passage||yard)) return false;
            Vector2 w=World(p);
            return !HitsBlocks(w,blockers)&&(!yard||!HitsBlocks(w,yardBlockers));
        }
        private static bool HitsBlocks(Vector2 w,BoxCollider2D[] items)
        {
            const float width=5.6f/32f, height=2.7f/32f;
            foreach(var b in items)
                if(b!=null && b.isActiveAndEnabled)
                {
                    var rect=b.bounds;
                    if(w.x+width>rect.min.x && w.x-width<rect.max.x &&
                       w.y+height>rect.min.y && w.y-height<rect.max.y) return true;
                }
            return false;
        }
        private void Carry(SpriteRenderer item)
        {
            Vector2 hold=facing==1?new Vector2(-15,-10):facing==2?new Vector2(15,-10)
                         :facing==3?new Vector2(11,-15):new Vector2(10,-3);
            item.transform.position=World(foot+hold);
            item.sortingOrder=dragon.sortingOrder+(facing==3?-1:1);
        }
        private void Think(int id,float seconds)
        {
            thoughtId=id; thoughtTill=Time.unscaledTime+seconds;
        }
        private SpriteRenderer GestureItem(int id) { return id==0?map:id==1?gloveProp:herbBasket; }
        private SpriteRenderer ChoreItem(int id) { return id==1?lantern:GestureItem(id); }
        private void QuietChores()
        {
            if(working<0) return;
            ChoreItem(working).transform.localRotation=workRotation;
            mages[working].sprite=IdleMage(working); working=-1;
        }
        private void ChoresUpdate()
        {
            choreClock+=Mathf.Min(Time.deltaTime,.05f);
            int id=((int)(choreClock/6.5f))%3;
            float phase=choreClock%6.5f;
            for(int i=0;i<3;i++)
            {
                if(IsAtVisitStation(i)) { if(working==i)QuietChores(); continue; }
                Vector2 currentSpot=At(mages[i].transform);
                Vector2 stand=At(indoorSpots[i]);
                Vector2 work=i==0?(mapped?new Vector2(187,198):new Vector2(170,188))
                    :i==1?new Vector2(282,214):new Vector2(110,178);
                bool close=Vector2.Distance(foot,currentSpot+new Vector2(0,12))<24f;
                bool onTask=i==id&&phase<4.5f;
                Vector2 target=onTask?work:stand;
                bool move=!close&&Vector2.Distance(currentSpot,target)>.7f;
                if(move)
                {
                    if(working==i) QuietChores();
                    Vector2 next=Vector2.MoveTowards(currentSpot,target,27f*Time.deltaTime);
                    if(ActorGap(next+new Vector2(0,12),foot)<1.15f)continue;
                    SetMage(i,next);
                    mages[i].sprite=MageFrames(i)[(int)(choreClock*8)%2];
                    continue;
                }
                if(onTask&&!close && phase>.9f)
                {
                    if(working!=i){QuietChores();working=i;workRotation=ChoreItem(i).transform.localRotation;}
                    mages[i].sprite=gestureSprites[i];
                    if(i!=0||!mapped)
                    {
                        float tilt=(i==1?10f:4f)*Mathf.Sin((phase-.9f)*4f);
                        ChoreItem(i).transform.localRotation=workRotation*Quaternion.Euler(0,0,tilt);
                    }
                }
                else if(working==i) QuietChores();
                else mages[i].sprite=IdleMage(i);
            }
        }
        private void Wake()
        {
            if(!asleep) return; asleep=false; SetDragon(At(landing)); dragon.sprite=WalkFrames()[0];
        }
        private void SleepOrFire(bool moving)
        {
            if(asleep) { idleSince=-1; dragon.sprite=sleepFrames[(int)(Time.unscaledTime*4)%4]; breath.enabled=false; return; }
            if(Time.time<fireUntil)
            {
                int i=Mathf.Clamp((int)((.50f-(fireUntil-Time.time))*8f),0,3);
                dragon.sprite=(facing==0?fireSouth:facing==1?fireWest:facing==2?fireEast:fireNorth)[i];
                breath.sprite=(facing==0?breathSouth:facing==1?breathWest:facing==2?breathEast:breathNorth)[i];
                breath.transform.position=World(foot+FireOffsets[facing]);
                breath.sortingOrder=dragon.sortingOrder+(facing==3?-1:1);
                breath.enabled=i>=2;
                return;
            }
            breath.enabled=false;
            if(moving){idleSince=-1;step+=Time.deltaTime*8f;dragon.sprite=WalkFrames()[(int)step%4];}
            else{step=0;dragon.sprite=IdleDragon();}
        }
        private void Flash()
        {
            Wake();
            idleSince=-1;
            hidden=false; dragon.enabled=true; fireUntil=Time.time+.50f; Play(puff);
        }

        private void Say(int[] ids,Action next=null)
        {
            queue=ids; current=0; afterLines=next;
            while(current<queue.Length&&!script.ContainsKey(queue[current])) current++;
            if(current==queue.Length) { mode=Mode.Walk; next?.Invoke(); return; }
            DismissHomeLook();
            mode=Mode.Lines;
        }
        private void NextLine()
        {
            current++;
            while(current<queue.Length&&!script.ContainsKey(queue[current])) current++;
            if(current<queue.Length) return;
            var end=afterLines; afterLines=null; mode=Mode.Walk; Play(click); end?.Invoke();
        }
        private void Pick(string q,params Choice[] c)
        {
            DismissHomeLook();
            question=q; options.Clear(); options.AddRange(c); options.Add(new Choice("Пока нет",()=>{})); mode=Mode.Choice;
        }
        private Choice Opt(string t,Action r) { return new Choice(t,r); }
        private void Select(Choice c)
        {
            mode=Mode.Walk; Play(click); c.run();
        }

        private string Near()
        {
            float best=28.1f; string found="";
            foreach(var t in targets)
            {
                if(!Allowed(t.id)) continue;
                Vector2 p=At(t.point);
                if(t.id=="window"&&(Mathf.Abs(foot.x-p.x)>8f||foot.y<p.y-1.5f||foot.y>p.y+8f))continue;
                if(t.id=="map"&&mapped) p=At(map.transform);
                if(t.id=="lantern") p=At(lantern.transform)+new Vector2(0,5);
                if(t.id=="cup") p=At(cup.transform);
                if(t.id=="star") p=At(hiddenStar.transform);
                if(t.id=="treat") p=At(treatPlate.transform);
                if(t.id=="run"||t.id=="iva"||t.id=="taya")
                    p=At(mages[t.id=="run"?0:t.id=="iva"?1:2].transform)+new Vector2(0,12);
                if(t.id=="berry") p=At(berry.transform);
                if(t.id=="button") p=At(button.transform);
                float dist=Vector2.Distance(p,foot);
                float limit=t.id=="star"?13.5f:t.id=="hearth"||t.id=="cup"?19f:28.1f;
                if(dist<best&&dist<=limit){best=dist;found=t.id;}
            }
            return found;
        }
        private bool Allowed(string id)
        {
            if(chasing) return false;
            if(lineupReady)return id=="run"||id=="iva"||id=="taya"||id=="door";
            if(id=="door") return !opened||lineupReady||CanGather;
            if(id=="window") return !viewedWindow&&!arrived;
            if(id=="treasure") return visitStep[0]==1?!heldCrystal:CanDescribeHome(1);
            if(id=="hearth") return CanDescribeHome(0);
            if(!arrived) return false;
            if(id=="star") return hiddenStar.enabled && visitStep[0]==1;
            if(id=="treat") return treatPlate.enabled && visitStep[2]==1;
            if(id=="lantern"&&LampTrial)return false;
            if(id=="cup") return cup.enabled&&CanDescribeHome(2);
            if(id=="basket") return carryPot||!finished.Contains(2);
            if(id=="bench") return stole || stashedBuckle;
            if(id=="berry") return berry.enabled&&!ate;
            if(id=="button") return button.enabled&&!hasButton;
            if(id=="pot" && (planted||carryPot)) return false;
            return true;
        }
        private void Interact()
        {
            if(chasing) return;
            if(hidden) { hidden=false;dragon.enabled=true;return; }
            if(asleep&&!opened) { Pick("Сегодня без гостей?",Opt("Остаться дома",()=>Finish(-1)));return; }
            if(TalkToInviter())return;
            string hit=Near();
            switch(hit)
            {
                case "door":
                    if(!opened) Pick("Открыть дверь?",Opt("Открыть",()=>StartCoroutine(GuestsEnter())),Opt("Остаться дома",()=>Finish(-1)));
                    else if(lineupReady) Pick("Проводить гостей?",Opt("Остаться дома",()=>Finish(-1)));
                    else if(CanGather) AskToGather();
                    break;
                case "window":
                    if(!viewedWindow&&!arrived){viewedWindow=true;Say(new[]{319});}
                    break;
                case "map": MapActions(); break;
                case "run": Character(0); break;
                case "iva": Character(1); break;
                case "taya": Character(2); break;
                case "gear": GearActions();break;
                case "lantern": LanternActions();break;
                case "basket": TaActions(); break;
                case "pot": TakePot(); break;
                case "bench": BenchActions(); break;
                case "button": TakeButton(); break;
                case "berry": ate=true;berry.enabled=false;Play(click);break;
                case "star": FindStar();break;
                case "treat": TreatMenu();break;
                case "cup":
                    LookAtHome(2);
                    break;
                case "treasure": CrystalActions(); break;
                case "hearth": LookAtHome(0); break;
            }
        }

        private void FireAction()
        {
            var selected=Near();
            if(CanBurnDrawing(selected))
            {
                Vector2 aim=At(map.transform)-foot;
                if(aim.sqrMagnitude>=1f)
                    facing=Mathf.Abs(aim.x)>Mathf.Abs(aim.y)?(aim.x<0?1:2):(aim.y<0?3:0);
                Flash();
                AlterMap(true);
                return;
            }
            Flash();
            if(chasing)return;
            if(!arrived) return;
            if(LampTrial){IgniteTrialLamp();return;}
            if((selected=="gear"||selected=="iva")&&glove)
            {
                if(!hasIntro[1]) hasIntro[1]=true;
                glove=false;
                Say(new[]{74,212},()=>Meet(1));
            }
            else if(selected=="lantern") LightLantern();
        }
        private bool CanBurnDrawing(string selected)
        {
            if(!arrived||evening||chasing||LampTrial||mapBurned||!map.enabled||foot.y<163f)return false;
            if(selected=="lantern"||selected=="gear"||(selected=="iva"&&glove))return false;
            Vector2 delta=At(map.transform)-foot;
            if(delta.sqrMagnitude>40f*40f)return false;
            return selected=="map"||delta.sqrMagnitude<=18f*18f
                ||(selected=="run"&&delta.sqrMagnitude<=27f*27f)||InFireCone(delta);
        }
        private void MapActions()
        {
            if(!mapped){AlterMap(false);return;}
            if(!finished.Contains(0)){ShareInterest(0);return;}
            if(MayVisit(0))VisitMenu(0);
        }
        private void AlterMap(bool burned)
        {
            if(burned?mapBurned:mapped)return;
            if(!mapped)
            {
                map.transform.position=World(new Vector2(186,202));
                map.sortingOrder=Depth(202);
            }
            mapped=true;mapBurned=burned;map.sprite=burned?burnedMap:firstMap;
            Say(burned?new[]{39,41}:new[]{45,47},()=>Meet(0));
        }
        private void GearActions()
        {
            if(ChaseOfferAvailable){VisitMenu(1);return;}
            var c=new List<Choice>();
            if(!hasIntro[1])
            {
                c.Add(Opt("Стащить пряжку",Steal));
                c.Add(Opt("Потрогать перчатку",()=>{glove=true;hasIntro[1]=true;Say(new[]{72},()=>Meet(1));}));
            }
            if(stole)
            {
                c.Add(Opt("Обменять пряжку",()=>ReturnBuckle(true)));
                c.Add(Opt("Вернуть пряжку",()=>ReturnBuckle(false)));
            }
            if(stashedBuckle) c.Add(Opt("Показать, где пряжка",()=>ReturnBuckle(false)));
            if(glove) c.Add(Opt("Пыхнуть",()=>{Flash();glove=false;Say(new[]{74,212});}));
            if(met.Contains(1)&&!heard.Contains(1)) c.Add(Opt("Рассмотреть фонарь",()=>ShareInterest(1)));
            if(MayVisit(1)||CanGather) c.Add(Opt("Поговорить с Ивой",()=>VisitMenu(1)));
            if(c.Count>0) Pick("Вещи Ивы",c.ToArray());
        }
        private void Steal()
        {
            hasIntro[1]=true;stole=true;CarrySmall(buckle,true);Say(new[]{59},()=>{ Meet(1);Think(232,7f); });
        }
        private void ReturnBuckle(bool trade)
        {
            stole=stashedBuckle=false;buckle.enabled=true;buckle.transform.position=buckleOrigin;buckle.sortingOrder=buckleOrder;
            if(trade) TakeButton();
            Say(new[]{trade?63:66});
        }
        private void TakeButton(){hasButton=true;button.enabled=false;Play(click);}
        private void LanternActions()
        {
            if(LampTrial)return;
            if(ChaseOfferAvailable){VisitMenu(1,fromLantern:MayVisit(1));return;}
            if(!lit)
            {
                Vector2 d=At(lantern.transform)-foot;
                facing=Mathf.Abs(d.x)>Mathf.Abs(d.y)?(d.x<0?1:2):(d.y<0?3:0);
                Flash();LightLantern();return;
            }
            if(!heard.Contains(1))ShareInterest(1);
        }
        private void LightLantern()
        {
            if(lit)return;
            Meet(1);hasIntro[1]=true;
            lit=true;
            bool heardBefore=heard.Contains(1);
            heard.Add(1);lantern.sprite=litLantern;
            Say(heardBefore?new[]{80}:new[]{213,79,80,214},()=>Complete(1));
        }
        private void TaActions()
        {
            if(carryPot)
            {
                Pick("Тая",Opt("Поставить горшок",PutPot));
                return;
            }
            if(finished.Contains(2))return;
            if(met.Contains(2))
            {
                Pick("Тая",Opt("Спросить про угощение",()=>ShareInterest(2)));
                return;
            }
            if(!mint)Pick("Корзинка Таи",Opt("Понюхать травы",()=>{
                mint=true;berry.enabled=true;
                berry.transform.position=World(new Vector2(128,184));
                Say(new[]{86},()=>Meet(2));
            }));
        }
        private void TakePot()
        {
            Pick("Горшок",Opt("Взять горшок",()=>carryPot=true));
        }
        private void PutPot()
        {
            carryPot=false;planted=true;potReady=Time.time+3.2f;pot.transform.position=World(new Vector2(107,183));pot.sortingOrder=Depth(185);
            Say(new[]{95},()=>Meet(2));
        }
        private void Meet(int id)
        {
            if(met.Add(id)&&met.Count==3)wishesPending=true;
        }
        private void Complete(int id)
        {
            if(!met.Contains(id)) return;
            if(!finished.Add(id)) return;
            if(finished.Count==1)
            {
                banterOwner=id;banterAt=Time.time+7f;
            }
            if(finished.Count==3 && !visitPhase && visitAt==0) visitAt=Time.time+1.2f;
        }
        private void ShareInterest(int id,Action next=null)
        {
            if(heard.Contains(id)) { next?.Invoke();return; }
            heard.Add(id);
            var lines=id==0 ? new[]{51,211,52} : id==1 ? new[]{213,79,214} : new[]{100,215,101};
            Say(lines,()=>{ Complete(id); next?.Invoke(); });
        }
        private void Character(int id)
        {
            if(lineupReady) { ChooseHand(id);return; }
            CharacterTalk(id);
        }
        private void CharacterTalk(int id)
        {
            if(id==2&&carryPot) { Pick("Тая",Opt("Поставить горшок",PutPot)); return; }
            if(finished.Contains(id)) { VisitMenu(id);return; }
            if(!evening)
            {
                if(id==0&&!mapped) MapActions();
                else if(id==1&&!met.Contains(1))
                {glove=true;hasIntro[1]=true;Say(new[]{72},()=>Meet(1));}
                else if(id==2&&!met.Contains(2)) TaActions();
                else ShareInterest(id);
                return;
            }
        }

        private IEnumerator GuestsEnter()
        {
            mode=Mode.Cut;
            SetDragon(new Vector2(224,174));
            opened=true;
            yield return Swing(true);
            Say(new[]{25},()=>StartCoroutine(GuestsWalkIn()));
        }
        private IEnumerator GuestsWalkIn()
        {
            mode=Mode.Cut;
            for(int i=0;i<3;i++)
            {
                yield return ArriveWithThings(i);
                mages[i].sprite=gestureSprites[i];
                bool introduced=false;
                Say(new[]{GuestIntroductions[i]},()=>{mode=Mode.Cut;introduced=true;});
                yield return new WaitUntil(()=>introduced);
                mages[i].sprite=IdleMage(i);
            }
            arrived=true;mode=Mode.Walk;Think(202,6.5f);
        }
        private IEnumerator Swing(bool opening)
        {
            for(int i=0;i<doorFrames.Length;i++)
            {
                door.sprite=doorFrames[opening?i:doorFrames.Length-1-i];
                yield return new WaitForSeconds(.055f);
            }
        }
        private IEnumerator MoveMage(int i,bool ride,params Vector2[] points)
        {
            int ix=0;
            foreach(var p in points)
                while (Vector2.Distance(At(mages[i].transform),p)>.5f)
                {
                    var move=Vector2.MoveTowards(At(mages[i].transform),p,135f*Time.deltaTime);
                    SetMage(i,move);
                    mages[i].sprite=MageFrames(i)[(ix++/7)%2];
                    if(ride) { asleep=false; dragon.sprite=walkEast[0]; SetDragon(move+new Vector2(8,-3)); }
                    yield return null;
                }
            mages[i].sprite=IdleMage(i);
        }

        private void Finish(int id)
        {
            if(id>=0&&!lineupReady)return;
            chosen=id;
            if(carryPot){carryPot=false;pot.transform.position=potOrigin;}
            if(stole||stashedBuckle){stole=stashedBuckle=false;buckle.enabled=true;buckle.transform.position=buckleOrigin;buckle.sortingOrder=buckleOrder;}
            ClearVisitForChoice();
            if(id<0)
            {
                var lines=opened?new[]{149}:new[]{152,153,323};
                Say(lines,()=>StartCoroutine(Stay()));
            }
            else StartCoroutine(SealChoice(id));
        }
        private IEnumerator Stay()
        {
            mode=Mode.Cut; asleep=true; dragon.enabled=true; SetDragon(At(perch)); dragon.sprite=sleepFrames[0];
            if(hasButton) { button.enabled=true;button.transform.position=gemOrigin+new Vector3(-.23f,0,0); }
            for(int i=0;i<3;i++)
            {
                if(opened) yield return MoveMage(i,false,new Vector2(208,174),new Vector2(208,116),new Vector2(251,64),new Vector2(266,-18));
                else yield return MoveMage(i,false,new Vector2(243,88),new Vector2(257,48),new Vector2(266,-18));
                mages[i].enabled=false;
            }
            if(opened) yield return Swing(false);
            bool hadGuests=opened;
            opened=false;
            if(hadGuests) Say(lineupReady?new[]{258,302}:new[]{258},()=>End(EndingKind.Home)); else End(EndingKind.Home);
        }
        private void End(EndingKind result)
        {
            StopGameplayMusic();
            endingKind=result;
            mode=Mode.End;
            breath.enabled=false;
        }

        private static Texture2D Flat(Color c)
        {
            var t=new Texture2D(1,1,TextureFormat.RGBA32,false);
            t.SetPixel(0,0,c);t.Apply();return t;
        }
        private static void StyleText(GUIStyle s,Color ink,bool transparent)
        {
            foreach(var state in new[]{s.normal,s.hover,s.active,s.focused,s.onNormal,s.onHover,s.onActive,s.onFocused})
            {
                state.textColor=ink;
                if(transparent)state.background=null;
            }
        }
        private void Styles()
        {
            if(body!=null) return;
            panel=Flat(new Color(.12f,.10f,.10f,.95f)); tint=Flat(new Color(.06f,.10f,.08f,.72f));
            body=new GUIStyle(){font=bodyFont,fontSize=21,wordWrap=true,alignment=TextAnchor.UpperLeft};
            StyleText(body,new Color(1f,.92f,.79f),true);
            title=new GUIStyle(body){font=titleFont,fontSize=45,alignment=TextAnchor.MiddleCenter};
            speaker=new GUIStyle(body){font=titleFont,fontSize=16}; StyleText(speaker,new Color(.99f,.74f,.44f),true);
            control=new GUIStyle(body){fontSize=14,alignment=TextAnchor.MiddleCenter};StyleText(control,new Color(.90f,.85f,.75f),true);
            speechBody=new GUIStyle(body){fontSize=17,wordWrap=false,clipping=TextClipping.Clip};
            narratorRows=new GUIStyle(body){fontSize=20,wordWrap=false,clipping=TextClipping.Clip};
            thoughtTitle=new GUIStyle(speaker){fontSize=14,fontStyle=FontStyle.Italic};
            StyleText(thoughtTitle,new Color(.71f,.87f,.68f),true);
            taskText=new GUIStyle(body){fontSize=14};
            StyleText(taskText,Color.white,true);
            taskDone=new GUIStyle(taskText){fontSize=12};StyleText(taskDone,new Color(.65f,.84f,.64f),true);
            ceremonyTitle=new GUIStyle(title){fontSize=30};
            taskNote=new GUIStyle(taskText){fontSize=12};StyleText(taskNote,new Color(.82f,.79f,.69f),true);
            timerDigits=new GUIStyle(control){font=titleFont,fontSize=28};StyleText(timerDigits,new Color(1f,.79f,.48f),true);
            thoughtPanel=Flat(new Color(.09f,.15f,.12f,.95f));
            option=new GUIStyle(){font=titleFont,fontSize=17,wordWrap=true,alignment=TextAnchor.MiddleCenter,
                border=new RectOffset(6,6,6,6),padding=new RectOffset(11,11,2,7)};
            option.normal.background=option.hover.background=option.focused.background=buttonNormal;
            option.onNormal.background=option.onHover.background=option.onFocused.background=buttonNormal;
            option.active.background=option.onActive.background=buttonPressed;
            StyleText(option,new Color(1f,.91f,.77f),false);
            SetMenuStyles();
        }
        private bool B(float x,float y,float w,string text){return GUI.Button(new Rect(x,y,w,42),text,option);}
        private void OnGUI()
        {
            Styles();
            var saved=GUI.matrix;
            float fit=Mathf.Min(Screen.width/832f,Screen.height/640f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-832*fit)*.5f,(Screen.height-640*fit)*.5f),Quaternion.identity,new Vector3(fit,fit,1));
            DrawCeremony(fit);
            if(mode==Mode.Title||mode==Mode.Pause||mode==Mode.End)
            {
                if(mode==Mode.Title)
                {
                    DrawTitleScreen(fit);
                }
                else if(mode==Mode.Pause)
                {
                    DrawWoodWindow(new Rect(206,148,420,332));
                    GUI.Label(new Rect(226,162,380,92),"Пауза",title);
                    DrawMusicLevel();
                    if(B(293,351,246,"Продолжить")) mode=Mode.Walk;
                    if(B(293,405,246,"В главное меню")) ResetGame();
                }
                else
                {
                    DrawEndingScreen(fit);
                }
            }
            else if(mode==Mode.Lines&&current<queue.Length&&script.TryGetValue(queue[current],out var l))
            {
                if(l.speaker!="Рассказчик")
                {
                    bool inner=l.speaker=="Дракончик";
                    DrawSpeech(SpeakerPosition(l),inner?"Дракончик · мысленно":l.speaker,l.text,fit,true,inner);
                }
                else
                {
                    DrawNarrator(l,fit);
                }
            }
            else if(mode==Mode.Choice)
            {
                float height=52f+options.Count*48f;
                DrawWoodWindow(new Rect(247,(640-height)/2,338,height+6));
                GUI.Label(new Rect(268,(640-height)/2+13,290,34),question,speaker);
                for(int i=0;i<options.Count;i++)
                    if(B(268,(640-height)/2+43+i*48f,296,options[i].text)) { var picked=options[i];Select(picked);break; }
            }
            else if(mode==Mode.Planting)DrawPlanting(fit);
            else if(mode==Mode.Walk)
            {
                DrawTaskPanel(fit);
                DrawVisitUi(fit);
                DrawLampTrial(fit);
                DrawControlsHint();
                if(DrawInvitation(fit)) { }
                else if(!hidden&&Time.unscaledTime<thoughtTill && script.TryGetValue(thoughtId,out var thought))
                {
                    DrawSpeech(dragon.transform.position,"Дракончик · мысленно",thought.text,fit,false,true);
                }
                else if(!evening&&banterOwner>=0&&Time.time>=banterAt&&Time.time<banterAt+8f &&
                        script.TryGetValue(221+banterOwner*2+(Time.time-banterAt>=4f?1:0),out var aside))
                    DrawSpeech(Anchor(aside.anchor),aside.speaker,aside.text,fit,false,false);
            }
            GUI.matrix=saved;
        }
        private void DrawControlsHint()
        {
            float remaining=controlsHintUntil-Time.unscaledTime;
            if(remaining<=0||Event.current.type!=EventType.Repaint)return;
            Color ink=GUI.color;
            GUI.color=new Color(ink.r,ink.g,ink.b,ink.a*Mathf.Clamp01(remaining/.65f));
            var hint=new Rect(156,580,520,36);
            GUI.DrawTexture(hint,thoughtPanel);
            GUI.Label(hint,ControlsHint,control);
            GUI.color=ink;
        }
        private static string MageName(int i) { return i==0?"Рун":i==1?"Ива":"Тая"; }
        private Vector3 Anchor(string anchor)
        {
            return anchor=="dragon"?dragon.transform.position:
                mages[anchor=="run"?0:anchor=="iva"?1:2].transform.position;
        }
        private void DrawSpeech(Vector3 anchor,string who,string value,float fit,bool formal,bool thought)
        {
            Vector2 p=UiPoint(anchor,fit);
            float visible=Screen.width/fit, width=Mathf.Min(visible-32f,thought||formal?366f:280f);
            var lines=WrapVisible(value,speechBody,width-36);
            float leading=CopyLeading(speechBody);
            float height=42f+lines.Count*leading;
            float x=Mathf.Clamp(p.x-width*.5f,416-visible*.5f+12,416+visible*.5f-width-12);
            float y=Mathf.Clamp(p.y-height-38,10,622-height);
            Texture2D background=thought?thoughtPanel:panel;
            GUI.DrawTexture(new Rect(x,y,width,height),background);
            float tail=Mathf.Clamp(p.x-5,x+9,x+width-18);
            for(int j=0;j<4;j++)
                GUI.DrawTexture(new Rect(tail+j,y+height+j*2,10-j*2,2),background);
            GUI.Label(new Rect(x+14,y+7,width-28,21),who,thought?thoughtTitle:speaker);
            PaintRows(lines,speechBody,x+14,y+28,width-28,leading);
        }
        private GUIStyle narratorRows;
        private static float CopyLeading(GUIStyle style)=>Mathf.Ceil(Mathf.Max(style.fontSize+6,style.CalcSize(new GUIContent("Аду")).y+4));
        private static List<string> WrapVisible(string value,GUIStyle style,float width)
        {
            var rows=new List<string>();
            var content=new GUIContent();
            foreach(string paragraph in value.Split('\n'))
            {
                string line="";
                foreach(string word in paragraph.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate=line.Length==0?word:line+" "+word;
                    content.text=candidate;
                    if(line.Length>0&&style.CalcSize(content).x>width){rows.Add(line);line=word;}
                    else line=candidate;
                }
                rows.Add(line);
            }
            return rows;
        }
        private static void PaintRows(List<string> rows,GUIStyle style,float x,float y,float width,float leading)
        {
            for(int i=0;i<rows.Count;i++)GUI.Label(new Rect(x,y+i*leading,width,leading),rows[i],style);
        }
        private void DrawNarrator(Line l,float fit)
        {
            float w=Mathf.Min(720,Screen.width/fit-32f),x=416-w*.5f;
            var rows=WrapVisible(l.text,narratorRows,w-58f);
            float leading=CopyLeading(narratorRows),h=54f+rows.Count*leading,y=609-h;
            GUI.DrawTexture(new Rect(x,y,w,h),panel);
            GUI.Label(new Rect(x+20,y+10,w-40,23),l.speaker,speaker);
            PaintRows(rows,narratorRows,x+20,y+37,w-40,leading);
        }
        private Vector2 UiPoint(Vector3 position,float fit)
        {
            Vector3 p=gameCamera.WorldToScreenPoint(position);
            return new Vector2((p.x-Screen.width*.5f)/fit+416f,(Screen.height*.5f-p.y)/fit+320f);
        }
    }
}
