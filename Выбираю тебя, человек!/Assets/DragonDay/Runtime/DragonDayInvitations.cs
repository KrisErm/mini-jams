using System.Collections.Generic;
using UnityEngine;

namespace DragonDay
{
    public sealed partial class DragonDayGame
    {
        private int inviting=-1, inviteTurn, inviteWaypoint, pathActor=-1;
        private float nextInvite, offerUntil;
        private bool walkingBack;
        private readonly List<Vector2> invitationPath=new List<Vector2>();
        private static readonly string[] InviteLines={
            "Эй, у меня есть игра с картой. Хочешь искать метки?",
            "Смотри, я принесла фонарики! Поможешь их зажечь?",
            "Поможешь посадить семена? Я покажу горшочки."
        };
        private static readonly Vector2[] InviteOffsets={
            new Vector2(-26,0),new Vector2(26,0),new Vector2(0,22),new Vector2(0,-21),
            new Vector2(-23,14),new Vector2(23,14),new Vector2(-23,-14),new Vector2(23,-14)
        };

        private Vector2 MageFoot(int id)=>At(mages[id].transform)+new Vector2(0,12);
        private static float ActorGap(Vector2 a,Vector2 b)
        {
            Vector2 d=a-b;
            return d.x*d.x/(11f*11f)+d.y*d.y/(7.5f*7.5f);
        }

        private void ResetInvitations()
        {
            inviting=pathActor=-1;inviteTurn=inviteWaypoint=0;
            nextInvite=offerUntil=0;walkingBack=false;
            invitationPath.Clear();
        }

        private void ReleaseInvitation()
        {
            if(inviting<0)return;
            mages[inviting].sprite=IdleMage(inviting);
            inviting=-1;walkingBack=false;offerUntil=0;
            invitationPath.Clear();nextInvite=Time.time+6.5f;
        }

        private void FindInvitationPath(Vector2 from,Vector2 destination,int id)
        {
            pathActor=id;
            for(int i=0;i<GridCount;i++)chaseGrid[i]=CanNavigate(ChasePoint(i));
            PlanChase(from,destination);
            pathActor=-1;
            invitationPath.Clear();invitationPath.AddRange(chaseRoute);
            inviteWaypoint=0;
        }

        private bool CanNavigate(Vector2 point)=>pathActor<0?CanCrossFloor(point):MageHasRoom(pathActor,point);

        private bool MageHasRoom(int id,Vector2 point)
        {
            if(!CanCrossFloor(point))return false;
            if(ActorGap(point,foot)<1.15f && ActorGap(point,foot)<=ActorGap(MageFoot(id),foot))return false;
            for(int i=0;i<mages.Length;i++)
                if(i!=id&&mages[i].enabled&&ActorGap(point,MageFoot(i))<1.15f &&
                   ActorGap(point,MageFoot(i))<=ActorGap(MageFoot(id),MageFoot(i)))return false;
            return true;
        }

        private bool CanInvite(int id)
        {
            if(id==1&&ChaseOfferAvailable)return true;
            return MayVisit(id)&&visitStep[id]==0;
        }

        private bool TickInvitation()
        {
            if(!visitPhase||evening)return false;
            if(inviting>=0)
            {
                if(!walkingBack && (!CanInvite(inviting)||(offerUntil>0 && (Time.time>=offerUntil||Vector2.Distance(foot,MageFoot(inviting))>42f))))
                {
                    walkingBack=true;offerUntil=0;
                    FindInvitationPath(MageFoot(inviting),At(indoorSpots[inviting])+new Vector2(0,12),inviting);
                }
                if(inviteWaypoint<invitationPath.Count)
                {
                    Vector2 at=MageFoot(inviting);
                    Vector2 next=Vector2.MoveTowards(at,invitationPath[inviteWaypoint],46f*Time.deltaTime);
                    if(!MageHasRoom(inviting,next))
                    {
                        ReleaseInvitation();return false;
                    }
                    SetMage(inviting,next-new Vector2(0,12));
                    mages[inviting].sprite=MageFrames(inviting)[(int)(Time.time*11)%2];
                    if(Vector2.Distance(next,invitationPath[inviteWaypoint])<.45f)inviteWaypoint++;
                    return true;
                }
                if(walkingBack||Vector2.Distance(MageFoot(inviting),foot)>43f)
                {ReleaseInvitation();return false;}
                if(offerUntil<=0)
                {
                    offerUntil=Time.time+6f;
                    mages[inviting].sprite=gestureSprites[inviting];
                }
                return true;
            }
            if(Time.time<nextInvite||foot.y<172||foot.y>261||visitStep[0]==1||LampTrial||SeedTrial)return false;
            for(int j=0;j<3;j++)
            {
                int id=(inviteTurn+j)%3;
                if(!CanInvite(id))continue;
                Vector2 at=MageFoot(id);
                if(Vector2.Distance(at,foot)<26f)
                {
                    inviting=id;inviteWaypoint=0;invitationPath.Clear();
                    offerUntil=Time.time+6f;inviteTurn=(id+1)%3;
                    return true;
                }
                foreach(Vector2 offset in InviteOffsets)
                {
                    Vector2 stop=foot+offset;
                    if(stop.y<173||!MageHasRoom(id,stop))continue;
                    FindInvitationPath(at,stop,id);
                    if(invitationPath.Count==0)continue;
                    inviting=id;inviteTurn=(id+1)%3;
                    return true;
                }
            }
            nextInvite=Time.time+2;
            return false;
        }

        private bool TalkToInviter()
        {
            if(inviting<0||walkingBack||offerUntil<=Time.time||Vector2.Distance(MageFoot(inviting),foot)>33f)return false;
            int id=inviting;
            ReleaseInvitation();
            VisitMenu(id);
            return true;
        }

        private bool DrawInvitation(float fit)
        {
            if(inviting<0||walkingBack||offerUntil<=Time.time)return false;
            string offer=inviting==1&&ChaseOfferAvailable?"Дракончик! Сыграем в догонялки?":InviteLines[inviting];
            DrawSpeech(mages[inviting].transform.position,MageName(inviting),offer,fit,false,false);
            return true;
        }
    }
}
