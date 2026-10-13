import QtQuick
import QtQuick.Shapes
import Dustan

// A quiet rotating arc for loading states.
Item {
    id: root
    property real size: Theme.s(28)
    property color color: Theme.accent
    property bool running: visible
    implicitWidth: size
    implicitHeight: size

    Shape {
        anchors.fill: parent
        preferredRendererType: Shape.CurveRenderer
        ShapePath {
            strokeColor: root.color
            strokeWidth: Math.max(2, root.size / 12)
            fillColor: "transparent"
            capStyle: ShapePath.RoundCap
            PathAngleArc {
                centerX: root.size / 2; centerY: root.size / 2
                radiusX: root.size / 2 - root.size / 12; radiusY: radiusX
                startAngle: 0; sweepAngle: 270
            }
        }
        RotationAnimator on rotation {
            from: 0; to: 360; duration: 900; loops: Animation.Infinite; running: root.running
        }
    }
}
