import QtQuick
import QtQuick.Shapes
import Dustan

// Line icon from Theme.icons, drawn as vector paths so it stays crisp at TV scale.
Item {
    id: root
    property string name
    property color color: Theme.text
    property real size: Theme.s(20)
    property real weight: 1.75

    implicitWidth: size
    implicitHeight: size
    readonly property var glyph: Theme.icons[name] || ({})

    Shape {
        width: 24
        height: 24
        scale: root.width / 24
        transformOrigin: Item.TopLeft
        preferredRendererType: Shape.CurveRenderer

        ShapePath {
            strokeColor: root.glyph.s ? root.color : "transparent"
            strokeWidth: root.weight
            fillColor: "transparent"
            capStyle: ShapePath.RoundCap
            joinStyle: ShapePath.RoundJoin
            PathSvg { path: root.glyph.s || "" }
        }
        ShapePath {
            strokeColor: "transparent"
            fillColor: root.glyph.f ? root.color : "transparent"
            fillRule: ShapePath.WindingFill
            PathSvg { path: root.glyph.f || "" }
        }
    }
}
