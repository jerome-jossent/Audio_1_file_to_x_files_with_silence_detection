﻿using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static System.Windows.Forms.AxHost;

namespace PanAndZoom
{
    public class ZoomBorder : Border
    {
        UIElement child = null;
        Point origin;
        Point start;

        bool X_fixed = true;

        TranslateTransform GetTranslateTransform(UIElement element)
        {
            return (TranslateTransform)((TransformGroup)element.RenderTransform)
              .Children.First(tr => tr is TranslateTransform);
        }

        ScaleTransform GetScaleTransform(UIElement element)
        {
            return (ScaleTransform)((TransformGroup)element.RenderTransform)
              .Children.First(tr => tr is ScaleTransform);
        }

        public override UIElement Child
        {
            get { return base.Child; }
            set
            {
                if (value != null && value != this.Child)
                    this.Initialize(value);
                base.Child = value;
            }
        }

        public void Initialize(UIElement element)
        {
            this.child = element;
            if (child != null)
            {
                TransformGroup group = new TransformGroup();
                ScaleTransform st = new ScaleTransform();
                group.Children.Add(st);
                TranslateTransform tt = new TranslateTransform();
                group.Children.Add(tt);
                child.RenderTransform = group;
                child.RenderTransformOrigin = new Point(0.0, 0.0);
                this.MouseWheel += child_MouseWheel;
                this.MouseLeftButtonDown += child_MouseLeftButtonDown;
                this.MouseLeftButtonUp += child_MouseLeftButtonUp;
                this.MouseMove += child_MouseMove;
                this.PreviewMouseRightButtonDown += new MouseButtonEventHandler(
                  child_PreviewMouseRightButtonDown);
            }
        }

        public void Reset()
        {
            if (child != null)
            {
                // reset zoom
                var st = GetScaleTransform(child);
                st.ScaleX = 1.0;
                st.ScaleY = 1.0;

                // reset pan
                var tt = GetTranslateTransform(child);
                tt.X = 0.0;
                tt.Y = 0.0;
            }
        }

        public void SetZoom(double relativeY, double aboluteZoom)
        {
            if (child != null)
            {
                // zoom
                var st = GetScaleTransform(child);
                st.ScaleY = aboluteZoom;

                // pan
                var tt = GetTranslateTransform(child);
                tt.Y = -relativeY * ActualHeight * aboluteZoom + ActualHeight / 2;
            }
        }

        public void SetRange(double relativeY_start, double relativeY_end)
        {
            if (child != null)
            {
                //vise le centre
                double y_moy = (relativeY_start + relativeY_end) / 2;
                double zoom = 1 / (relativeY_end - relativeY_start);
                SetZoom(y_moy, zoom);
            }
        }

        #region Child Events

        private void child_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (child != null)
            {
                var st = GetScaleTransform(child);
                var tt = GetTranslateTransform(child);

                //double zoom = e.Delta > 0 ? .2 : -.2;
                double zoom = e.Delta > 0 ? 1.2 : .8;
                if (!(e.Delta > 0) && (st.ScaleX < .4 || st.ScaleY < .4))
                    return;

                Point relative = e.GetPosition(child);
                double absoluteX = 0;
                double absoluteY = 0;

                if (!X_fixed)
                    absoluteX = relative.X * st.ScaleX + tt.X;

                absoluteY = relative.Y * st.ScaleY + tt.Y;

                if (!X_fixed)
                    st.ScaleX += zoom;

                st.ScaleY *= zoom;

                //limit dezoom
                if (st.ScaleY < 1)
                    st.ScaleY = 1;

                if (!X_fixed)
                    tt.X = absoluteX - relative.X * st.ScaleX;

                tt.Y = absoluteY - relative.Y * st.ScaleY;
            }
        }

        private void child_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (child != null)
            {
                var tt = GetTranslateTransform(child);
                start = e.GetPosition(this);

                double X = X_fixed ? origin.X : tt.X;


                origin = new Point(tt.X, tt.Y);


                this.Cursor = Cursors.Hand;
                child.CaptureMouse();
            }
        }

        private void child_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (child != null)
            {
                child.ReleaseMouseCapture();
                this.Cursor = Cursors.Arrow;
            }
        }

        void child_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Reset();
        }

        public delegate void SampleEventHandler(object sender, SampleEventArgs e);
        public static event SampleEventHandler SampleEvent;

        private void child_MouseMove(object sender, MouseEventArgs e)
        {
            if (child != null)
            {
                Point mouse = e.GetPosition(this);
                var st = GetScaleTransform(child);
                var tt = GetTranslateTransform(child);
                if (child.IsMouseCaptured)
                {
                    Vector v = start - mouse;
                    if (!X_fixed)
                        tt.X = origin.X - v.X;

                    tt.Y = origin.Y - v.Y;

                }
                // Point mouse2 = e.GetPosition(this);
                SampleEvent?.Invoke(this, new SampleEventArgs(st.ScaleY, tt.Y / ActualHeight, mouse.Y / ActualHeight));
            }
        }
        #endregion
    }

    public class SampleEventArgs
    {
        public double scaleY { get; }
        public double relativeoffsetY { get; }
        public double mouseRelativeY { get; }

        public SampleEventArgs(double scale, double offset, double mouse)
        {
            this.scaleY = scale;
            this.relativeoffsetY = offset;
            this.mouseRelativeY = mouse;
        }
    }
}