shader_type canvas_item;

varying vec2 local_position;

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    // AtlasTexture的UV指向原始插画；圆形遮罩必须按90×90控件局部坐标求值。
    vec4 pixel = texture(TEXTURE, UV);
    float radius = length((local_position - vec2(45.0)) / 90.0);
    pixel.a *= 1.0 - smoothstep(0.49, 0.5, radius);
    COLOR = pixel * COLOR;
}
