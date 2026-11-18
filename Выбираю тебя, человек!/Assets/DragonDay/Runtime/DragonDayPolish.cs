using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DragonDay
{
    public sealed partial class DragonDayGame
    {
        [Header("Arrival, idle motion and task panel")]
        [SerializeField] private SpriteRenderer localResident, ivaBag, portraitPage;
        [SerializeField] private BoxCollider2D[] guestColliders;
        [SerializeField] private Sprite[] idleSouth,idleWest,idleEast,idleNorth,fireSouth,fireNorth,breathSouth,breathNorth;
        [SerializeField] private Sprite pledgePaw;
        [SerializeField] private AudioClip vowChime;

        private readonly Vector3[] guestItemPosition = new Vector3[3];
        private readonly int[] guestItemOrder = new int[3];
        private readonly bool[] sideMoment = new bool[3];
        private int carryingIn = -1;
        private float idleSince=-1, steamUntil, burstAt, burstLife;
        private bool ceremonyBurst;
        private int burstMage;
        private Vector3 burstWorld;
        private static readonly Vector2[] FireOffsets={new Vector2(0,14),new Vector2(-33,-7),new Vector2(33,-7),new Vector2(0,-36)};
        private static readonly float[] IdleBeats={1.04f,1.18f,1.31f,1.50f,1.63f,1.84f,3.47f,3.59f,3.76f,3.90f,4.38f};
        private static readonly int[] IdlePoses={0,1,2,3,4,1,0,3,4,1,5};

        private SpriteRenderer ArrivalItem(int i) => i == 0 ? map : i == 1 ? ivaBag : herbBasket;

        private void InitializePolish()
        {
            for (int i = 0; i < 3; i++)
            {
                SpriteRenderer item = ArrivalItem(i);
                guestItemPosition[i] = item.transform.position;
                guestItemOrder[i] = item.sortingOrder;
            }
        }

        private void ResetPolish()
        {
            carryingIn=-1; idleSince=-1; steamUntil=burstLife=0;
            Array.Clear(sideMoment,0,sideMoment.Length);
            portraitPage.enabled=false;
            for (int i = 0; i < 3; i++)
                PutGuestThings(i, false);
        }

        private void PutGuestThings(int id, bool present)
        {
            var item = ArrivalItem(id);
            item.enabled = present;
            item.transform.position = guestItemPosition[id];
            item.sortingOrder = guestItemOrder[id];
            if (id != 1) return;
            lantern.enabled = buckle.enabled = gloveProp.enabled = present;
            foreach (var c in guestColliders) c.enabled = present;
        }

        private void UpdateArrivalItem(int id)
        {
            if (carryingIn != id) return;
            var item = ArrivalItem(id);
            item.transform.position = mages[id].transform.position + new Vector3(.20f,-.15f,0);
            item.sortingOrder = mages[id].sortingOrder+2;
        }

        private IEnumerator ArriveWithThings(int id)
        {
            carryingIn = id;
            ArrivalItem(id).enabled = true;
            UpdateArrivalItem(id);
            if (id == 0)
                yield return MoveMage(id,false,new Vector2(208,116),new Vector2(208,164),new Vector2(208,192),new Vector2(208,207),At(indoorSpots[id]));
            else if (id == 2)
                yield return MoveMage(id,false,new Vector2(208,116),new Vector2(208,164),new Vector2(208,207),new Vector2(150,199),At(indoorSpots[id]));
            else
                yield return MoveMage(id,false,new Vector2(208,116),new Vector2(208,164),new Vector2(208,185),new Vector2(259,211),At(indoorSpots[id]));
            carryingIn = -1;
            mages[id].sprite = gestureSprites[id];
            PutGuestThings(id,true);
            yield return new WaitForSeconds(.27f);
            mages[id].sprite = IdleMage(id);
        }

        private Sprite IdleDragon()
        {
            if(idleSince<0)idleSince=Time.time;
            float t=(Time.time-idleSince)%IdleBeats[IdleBeats.Length-1];
            int beat=0;
            while(beat<IdleBeats.Length-1 && t>=IdleBeats[beat])beat++;
            return (facing==0?idleSouth:facing==1?idleWest:facing==2?idleEast:idleNorth)[IdlePoses[beat]];
        }

        private void AddPersonalMoment(List<Choice> options,int id)
        {
            if(!finished.Contains(id)||sideMoment[id])return;
            if(id==0)options.Add(Opt("Посмотреть портрет",()=>StartCoroutine(SketchWithRun())));
            if(id==1)options.Add(Opt("Попросить показать фокус",()=>StartCoroutine(SparkWithIva())));
            if(id==2&&!SeedTrial)options.Add(Opt("Попить чаю с Таей",()=>StartCoroutine(TeaWithTaya())));
        }

        private IEnumerator SketchWithRun()
        {
            if(sideMoment[0])yield break;
            sideMoment[0]=true; mode=Mode.Cut;
            portraitPage.transform.position=mages[0].transform.position+new Vector3(.37f,-.15f,0);
            portraitPage.sortingOrder=mages[0].sortingOrder+2;
            portraitPage.enabled=true; mages[0].sprite=gestureSprites[0];
            yield return new WaitForSeconds(.32f);
            Say(new[]{292,293},()=>{
                portraitPage.enabled=false;
                mages[0].sprite=IdleMage(0);
            });
        }

        private IEnumerator SparkWithIva()
        {
            if(sideMoment[1])yield break;
            sideMoment[1]=true; mode=Mode.Cut;
            mages[1].sprite=gestureSprites[1];
            Burst(mages[1].transform.position+new Vector3(.23f,-.06f,0),1,1.8f,false);
            Play(puff);
            yield return new WaitForSeconds(.52f);
            Say(new[]{294,295},()=>mages[1].sprite=IdleMage(1));
        }

        private IEnumerator TeaWithTaya()
        {
            if(sideMoment[2])yield break;
            sideMoment[2]=true; mode=Mode.Cut;
            yield return MoveMage(2,false,new Vector2(140,194),new Vector2(165,202),new Vector2(167,233));
            cup.enabled=true;
            cup.transform.position=World(new Vector2(188,248));
            cup.sortingOrder=Depth(268)+1;
            steamUntil=Time.time+15f; mages[2].sprite=gestureSprites[2];
            yield return new WaitForSeconds(.42f);
            Say(new[]{296,297},()=>StartCoroutine(TayaReturns()));
        }

        private IEnumerator TayaReturns()
        {
            mode=Mode.Cut;steamUntil=0;
            cup.transform.position=cupOrigin;cup.sortingOrder=cupOrder;
            yield return MoveMage(2,false,new Vector2(165,202),new Vector2(140,194),At(indoorSpots[2]));
            mode=Mode.Walk;
        }

        private string ActiveTask()
        {
            if (lineupReady) return "Выбери мага или останься дома.";
            if (!opened) return viewedWindow ? "Реши, впускать ли гостей." : "Гости на пороге.";
            if (!arrived) return "Впусти гостей.";
            if (!visitPhase) return "Познакомься с тремя магами   "+finished.Count+"/3";
            if (CanGather) return "Позови гостей к двери или поиграй ещё.";
            return "Попробуй занятия гостей  "+visitedTogether.Count+"/2";
        }

        private string GuestTask(int id)
        {
            if(!visitPhase)
            {
                if(finished.Contains(id))return id==0?"Рун — посмотрели рисунок":id==1?"Ива — обсудили фонарь":"Тая — узнали про травы";
                if(id==0)return mapped?"Рун — поговорить о рисунке":"Рун — узнать, что на рисунке";
                if(id==1)return met.Contains(1)?"Ива — рассмотреть фонарь":"Ива — посмотреть на перчатку";
                return met.Contains(2)?"Тая — поговорить об угощении":"Тая — узнать, что в корзинке";
            }
            if(visitedTogether.Contains(id))
                return id==0?"Рун — звезда найдена":id==1?"Ива — фонари зажжены":"Тая — семена посажены";
            if(id==0)return visitStep[0]==0?"Рун — спросить про карту":!heldCrystal?"Рун — взять камешек из сундука":"Рун — найти бумажные метки  "+huntMark+"/2";
            if(id==1)return LampTrial?"Ива — зажечь фонарики  "+lampCount+"/3":"Ива — узнать про фонарики";
            return SeedTrial?"Тая — посадить семена  "+plantedSeeds+"/3":"Тая — спросить про семена";
        }

        private readonly string[] taskRows=new string[3];
        private readonly float[] taskHeights=new float[3];
        private readonly bool[] taskResolved=new bool[3];
        private readonly GUIContent taskMeasure=new GUIContent();

        private float TaskHeight(string value,GUIStyle style,float width)
        {
            taskMeasure.text=value;
            return Mathf.Max(17,Mathf.Ceil(style.CalcHeight(taskMeasure,width)));
        }

        private void DrawTaskPanel(float fit)
        {
            if (mode != Mode.Walk || chasing) return;
            float x = (832f-Screen.width/fit)*.5f+14f;
            float width=Mathf.Min(299f,Screen.width/fit*.5f-20f), inside=width-20f;
            bool showGuests=arrived&&!lineupReady;
            string main=ActiveTask();
            float mainHeight=TaskHeight(main,taskText,inside);
            float y=41f, bottom=y+mainHeight;
            if (showGuests)
            {
                bottom+=7f;
                for(int row=0;row<3;row++)
                {
                    int id=(row+2)%3;
                    taskResolved[row]=visitPhase?visitedTogether.Contains(id):finished.Contains(id);
                    taskRows[row]=(taskResolved[row]?"✓  ":"·  ")+GuestTask(id);
                    taskHeights[row]=TaskHeight(taskRows[row],taskResolved[row]?taskDone:taskNote,inside);
                    bottom+=taskHeights[row]+4f;
                }
            }
            GUI.DrawTexture(new Rect(x,12,width,bottom-12+8f),panel);
            GUI.Label(new Rect(x+10,19,inside,20),"Задачи",speaker);
            GUI.Label(new Rect(x+10,y,inside,mainHeight),main,taskText);
            if(!showGuests)return;
            y+=mainHeight+7f;
            for(int row=0;row<3;row++)
            {
                GUI.Label(new Rect(x+10,y,inside,taskHeights[row]),taskRows[row],taskResolved[row]?taskDone:taskNote);
                y+=taskHeights[row]+4f;
            }
        }

        private Vector3 SpeakerPosition(Line l)
        {
            if (l.speaker == "Местный житель") return localResident.transform.position;
            if (l.speaker == "Дракончик") return dragon.transform.position;
            if (l.speaker == "Рун") return mages[0].transform.position;
            if (l.speaker == "Ива") return mages[1].transform.position;
            if (l.speaker == "Тая") return mages[2].transform.position;
            return Anchor(l.anchor);
        }

        private void Burst(Vector3 point,int id,float duration,bool vows)
        {
            burstWorld=point;burstMage=id;burstAt=Time.time;burstLife=duration;ceremonyBurst=vows;
        }

        private void DrawCeremony(float fit)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var ink=GUI.color;
            if(steamUntil>Time.time)
            {
                Vector2 p=UiPoint(cup.transform.position,fit);
                for(int i=0;i<3;i++)
                {
                    float d=(Time.time*.9f+i/3f)%1f;
                    GUI.color=new Color(1f,.89f,.76f,(1f-d)*.58f);
                    GUI.DrawTexture(new Rect(p.x-3f+Mathf.Sin(4*d+i)*4,p.y-14f-22*d,3,3),Texture2D.whiteTexture);
                }
            }
            float age=Time.time-burstAt;
            if(burstLife>0&&age<burstLife&&mode!=Mode.End)
            {
                Vector2 p=UiPoint(burstWorld,fit);
                float progress=age/burstLife, fade=Mathf.Min(progress*9f,(1f-progress)*4f);
                float width=Screen.width/fit,height=Screen.height/fit;
                if(ceremonyBurst)
                {
                    GUI.color=new Color(.14f,.09f,.14f,fade*.29f);
                    GUI.DrawTexture(new Rect(416-width*.5f,320-height*.5f,width,height),Texture2D.whiteTexture);
                }
                Color hue=burstMage==0?new Color(1f,.79f,.38f):burstMage==1?new Color(1f,.63f,.42f):new Color(.74f,1f,.63f);
                for(int i=0;i<(ceremonyBurst?26:12);i++)
                {
                    float phase=(progress*.92f+i*.249f)%1f, a=i*2.39996f+age*.9f;
                    float distance=(ceremonyBurst?34f:12f)+phase*(ceremonyBurst?76f:48f);
                    float x=p.x+Mathf.Cos(a)*distance, y=p.y+Mathf.Sin(a)*distance*.49f-46*phase;
                    GUI.color=new Color(hue.r,hue.g,hue.b,fade*(1f-phase));
                    float k=i%4==0?7f:3f;
                    GUI.DrawTexture(new Rect(x-k*.5f,y-1,k,2),Texture2D.whiteTexture);
                    if(i%4==0)GUI.DrawTexture(new Rect(x-1,y-k*.5f,2,k),Texture2D.whiteTexture);
                }
                float starY=p.y-(ceremonyBurst?12f:22f)-Mathf.SmoothStep(0,22,progress);
                GUI.color=new Color(1f,.95f,.70f,fade);
                GUI.DrawTexture(new Rect(p.x-1.5f,starY-9f,3f,18f),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(p.x-7f,starY-1.5f,14f,3f),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(p.x-3.5f,starY-3.5f,7f,7f),Texture2D.whiteTexture);
                if(ceremonyBurst&&age>.6f)
                {
                    GUI.color=new Color(1,1,1,Mathf.Min((age-.6f)*2f,fade));
                    GUI.DrawTexture(new Rect(219,77,394,46),panel);
                    GUI.Label(new Rect(222,76,388,47),"Сегодня тот самый день",ceremonyTitle);
                }
            }
            GUI.color=ink;
        }

        private GUIStyle taskText,taskNote,taskDone,timerDigits,ceremonyTitle;
    }
}
