using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Quartz.UI.Behaviors
{
    public class FlyoutAssist
    {
        // Регистрируем Attached Property для сдвига по X
        public static readonly AttachedProperty<double> HorizontalOffsetProperty =
            AvaloniaProperty.RegisterAttached<MenuFlyoutPresenter, MenuFlyoutPresenter, double>("HorizontalOffset");

        // Регистрируем Attached Property для сдвига по Y
        public static readonly AttachedProperty<double> VerticalOffsetProperty =
            AvaloniaProperty.RegisterAttached<MenuFlyoutPresenter, MenuFlyoutPresenter, double>("VerticalOffset");

        public static void SetHorizontalOffset(AvaloniaObject element, double value) => element.SetValue(HorizontalOffsetProperty, value);
        public static double GetHorizontalOffset(AvaloniaObject element) => element.GetValue(HorizontalOffsetProperty);

        public static void SetVerticalOffset(AvaloniaObject element, double value) => element.SetValue(VerticalOffsetProperty, value);
        public static double GetVerticalOffset(AvaloniaObject element) => element.GetValue(VerticalOffsetProperty);

        static FlyoutAssist()
        {
            HorizontalOffsetProperty.Changed.AddClassHandler<MenuFlyoutPresenter>(OnOffsetChanged);
            VerticalOffsetProperty.Changed.AddClassHandler<MenuFlyoutPresenter>(OnOffsetChanged);
        }

        private static void OnOffsetChanged(MenuFlyoutPresenter presenter, AvaloniaPropertyChangedEventArgs e)
        {
            // Когда презентер загружается в дерево, ищем его Popup и сдвигаем
            presenter.Loaded += (s, ev) =>
            {
                if (presenter.Parent is Popup popup)
                {
                    popup.HorizontalOffset = GetHorizontalOffset(presenter);
                    popup.VerticalOffset = GetVerticalOffset(presenter);
                }
            };
        }
    }
}