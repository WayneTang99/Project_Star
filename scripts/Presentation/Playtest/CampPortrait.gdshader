shader_type canvas_item;

varying vec2 local_position;

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    // 营地65像素圆形肖像按局部坐标裁切，兼容正式原画的AtlasTexture取景。
    float radius = length(local_position / 65.0 - vec2(0.5));
    float edge = max(fwidth(radius), 0.001);
    COLOR.rgb = mix(COLOR.rgb, vec3(0.78, 0.67, 0.44), smoothstep(0.47 - edge, 0.47 + edge, radius));
    COLOR.a *= 1.0 - smoothstep(0.49 - edge * 0.5, 0.49 + edge * 0.5, radius);
}
