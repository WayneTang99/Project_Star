shader_type canvas_item;

varying vec2 local_position;

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    // AtlasTexture的UV指向原始插画；圆形遮罩必须按90×90控件局部坐标求值。
    float radius = length((local_position - vec2(45.0)) / 90.0);
    // fragment 的 COLOR 已含纹理颜色，只裁透明度，避免重复相乘改变色相与亮度。
    float edge = max(fwidth(radius), 0.001);
    COLOR.a *= 1.0 - smoothstep(0.43 - edge * 0.5, 0.43 + edge * 0.5, radius);
}
