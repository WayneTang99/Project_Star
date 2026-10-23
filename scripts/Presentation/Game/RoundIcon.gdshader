shader_type canvas_item;
void fragment() {
    float radius = length(UV - vec2(0.5));
    float edge = max(fwidth(radius), 0.001);
    COLOR.a *= 1.0 - smoothstep(0.49 - edge * 0.5, 0.49 + edge * 0.5, radius);
}
