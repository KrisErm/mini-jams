using UnityEngine;

namespace DragonDay
{
    public sealed partial class DragonDayGame
    {
        [Header("Title screen and wooden menu")]
        [SerializeField] private Texture2D titleArtwork, woodPanel, woodInset, woodTrack, woodGrabber;
        [SerializeField] private Texture2D[] endingArtwork;
        [SerializeField] private Font storybookFont;
        private GUIStyle woodFrameStyle,woodInsetStyle,woodTrackStyle,woodGrabberStyle;
        private GUIStyle titleLead,titleHero,shadowLead,shadowHero,titleSubtitle,endingHeading,endingShadow;
        private Texture2D titleRule;
        private enum EndingKind { Run=0, Iva=1, Taya=2, Home=3 }
        private EndingKind endingKind;
        private static readonly string[] EndingHeadings={"С Руном","С Ивой","С Таей","Дома"};

        private void SetMenuStyles()
        {
            woodFrameStyle=SlicedStyle(woodPanel,4,4,4,4);
            woodInsetStyle=SlicedStyle(woodInset,4,4,4,4);
            woodTrackStyle=SlicedStyle(woodTrack,3,3,2,2);
            woodTrackStyle.fixedHeight=10;
            woodGrabberStyle=SlicedStyle(woodGrabber,0,0,0,0);
            woodGrabberStyle.fixedWidth=14;
            woodGrabberStyle.fixedHeight=22;
            titleLead=new GUIStyle(title){font=storybookFont,fontSize=48,wordWrap=false};
            titleHero=new GUIStyle(titleLead){fontSize=66};
            shadowLead=new GUIStyle(titleLead);
            shadowHero=new GUIStyle(titleHero);
            titleSubtitle=new GUIStyle(control){font=storybookFont,fontSize=18,wordWrap=false};
            StyleText(titleLead,new Color(1f,.92f,.75f),true);
            StyleText(titleHero,new Color(1f,.84f,.56f),true);
            StyleText(shadowLead,new Color(.12f,.075f,.038f,.94f),true);
            StyleText(shadowHero,new Color(.12f,.075f,.038f,.94f),true);
            StyleText(titleSubtitle,new Color(.94f,.84f,.64f),true);
            endingHeading=new GUIStyle(titleHero){fontSize=47};
            endingShadow=new GUIStyle(shadowHero){fontSize=47};
            titleRule=Flat(new Color(.97f,.72f,.36f,.73f));
        }

        private static GUIStyle SlicedStyle(Texture2D art,int left,int right,int top,int bottom)
        {
            var s=new GUIStyle(){border=new RectOffset(left,right,top,bottom)};
            s.normal.background=s.hover.background=s.active.background=s.focused.background=art;
            s.onNormal.background=s.onHover.background=s.onActive.background=s.onFocused.background=art;
            return s;
        }

        private void DrawTitleArtwork(float fit)
        {
            DrawCover(fit,titleArtwork);
        }

        private Rect DrawCover(float fit,Texture2D artwork)
        {
            float w=Screen.width/fit,h=Screen.height/fit;
            var view=new Rect(416f-w*.5f,320f-h*.5f,w,h);
            // Fit instead of cropping to keep the dragon and the apprentices in frame.
            if(Event.current.type==EventType.Repaint)GUI.DrawTexture(view,thoughtPanel);
            if(artwork==null)return view;
            float width=Mathf.Min(w,h*artwork.width/artwork.height);
            float height=width*artwork.height/artwork.width;
            var picture=new Rect(416f-width*.5f,320f-height*.5f,width,height);
            if(Event.current.type==EventType.Repaint)GUI.DrawTexture(picture,artwork,ScaleMode.ScaleToFit);
            return picture;
        }

        private static void DrawTitleLine(Rect rect,string text,GUIStyle face,GUIStyle shadow)
        {
            GUI.Label(new Rect(rect.x+2f,rect.y+3f,rect.width,rect.height),text,shadow);
            GUI.Label(rect,text,face);
        }

        private void DrawTitleScreen(float fit)
        {
            DrawTitleArtwork(fit);
            DrawTitleLine(new Rect(151,121,530,72),"Выбираю тебя,",titleLead,shadowLead);
            DrawTitleLine(new Rect(151,184,530,84),"человек!",titleHero,shadowHero);
            if(Event.current.type==EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(356,276,50,1),titleRule);
                GUI.DrawTexture(new Rect(426,276,50,1),titleRule);
                GUI.DrawTexture(new Rect(413,275,6,3),titleRule);
                GUI.DrawTexture(new Rect(414,274,4,5),titleRule);
            }
            GUI.Label(new Rect(201,290,430,31),"Сегодня тот самый день",titleSubtitle);
            if(B(310,363,212,"Начать"))StartStory();
            if(B(310,422,212,"Выход"))Application.Quit();
        }

        private void DrawEndingScreen(float fit)
        {
            int index=(int)endingKind;
            var art=endingArtwork!=null&&index<endingArtwork.Length?endingArtwork[index]:null;
            Rect picture=DrawCover(fit,art);
            float center=picture.x+picture.width*.176f;
            float column=picture.width*.315f;
            float buttons=Mathf.Min(228f,column-12f);
            float headingY=picture.y+picture.height*.34f;
            float buttonY=picture.y+picture.height*.56f;
            GUI.Label(new Rect(center-column*.5f,headingY-25f,column,28f),"Финал",titleSubtitle);
            DrawTitleLine(new Rect(center-column*.5f,headingY,column,64f),EndingHeadings[index],endingHeading,endingShadow);
            if(Event.current.type==EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(center-53f,headingY+72f,46f,1f),titleRule);
                GUI.DrawTexture(new Rect(center+7f,headingY+72f,46f,1f),titleRule);
                GUI.DrawTexture(new Rect(center-2f,headingY+70f,4f,5f),titleRule);
            }
            if(B(center-buttons*.5f,buttonY,buttons,"Сыграть ещё раз"))StartStory();
            if(B(center-buttons*.5f,buttonY+55f,buttons,"В главное меню"))ResetGame();
        }

        private void DrawWoodWindow(Rect window)
        {
            if(Event.current.type!=EventType.Repaint)return;
            woodFrameStyle.Draw(window,GUIContent.none,false,false,false,false);
            var inside=new Rect(window.x+9,window.y+10,window.width-18,window.height-20);
            woodInsetStyle.Draw(inside,GUIContent.none,false,false,false,false);
        }
    }
}
