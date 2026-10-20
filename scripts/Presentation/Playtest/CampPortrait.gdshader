shader_type canvas_item;

varying vec2 local_position;
uniform float portrait_size = 65.0;

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    // 按控件逻辑尺寸裁切圆形肖像，兼容AtlasTexture取景与图鉴小头像。
    float radius = length(local_position / portrait_size - vec2(0.5));
    float edge = max(fwidth(radius), 0.001);
    COLOR.rgb = mix(COLOR.rgb, vec3(0.78, 0.67, 0.44), smoothstep(0.47 - edge, 0.47 + edge, radius));
    COLOR.a *= 1.0 - smoothstep(0.49 - edge * 0.5, 0.49 + edge * 0.5, radius);
}
