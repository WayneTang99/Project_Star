shader_type canvas_item;

// 保留古铜与砂岩，只把底图中残留的冷色外围按原亮度转为暖灰。
void fragment() {
    vec4 pixel = COLOR;
    float cool = smoothstep(0.0, 0.015, max(pixel.g, pixel.b) - pixel.r);
    float luminance = dot(pixel.rgb, vec3(0.299, 0.587, 0.114));
    COLOR = vec4(mix(pixel.rgb, luminance * vec3(1.16, 0.99, 0.84), cool), pixel.a);
}
