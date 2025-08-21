#version 410 core

precision highp float;

layout(location = 0) out vec4 o_color;

in vec3 near;
in vec3 far;

uniform float u_nearfar[2];
uniform mat4 view;
uniform mat4 projection;

// Enhanced grid function with better visibility
vec4 grid(vec3 point, float scale, bool is_axis) {
    // Apply the 0.5 offset here
    vec2 coord = (point.xz + vec2(0.5, 0.5)) * scale;
    
    vec2 derivative = fwidth(coord);
    vec2 grid = abs(fract(coord - 0.5) - 0.5) / derivative;
    float line = min(grid.x, grid.y);
    float minimumx = min(derivative.x, 1.0);
    float minimumz = min(derivative.y, 1.0);
    
    // Start with a darker base color
    vec4 color = vec4(0.2, 0.2, 0.2, 1.0 - min(line, 1.0));
    
    // Highlight axes with more prominent colors - adjust for the offset
    if (abs(point.x + 0.5) < minimumx * 0.5)
        color.rgb = vec3(0.427, 0.792, 0.909); // Blue for X axis
    if (abs(point.z + 0.5) < minimumz * 0.5)
        color.rgb = vec3(0.984, 0.380, 0.490); // Red for Z axis
    
    // Make grid lines more visible with higher contrast
    if (color.a < 0.1)
        discard;
        
    return color;
}

float compute_depth(vec3 point) {
    vec4 clip = projection * view * vec4(point, 1.0);
    return (clip.z / clip.w) * 0.5 + 0.5;
}

float compute_fade(vec3 point) {
    float dist = length(point); // Simple distance-based fade
    float nearPlane = u_nearfar[0];
    float farPlane = u_nearfar[1];
    return 1.0 - clamp(dist / farPlane, 0.0, 1.0);
}

void main() {
    // Intersect ray with horizontal plane at y=-0.5 instead of y=0
    float t = -(near.y + 0.5) / (far.y - near.y);
    
    // Only draw grid if intersection is in front of the camera
    if (t <= 0.0) {
        discard;
    }
    
    vec3 R = near + t * (far - near);
    
    // Rest of your code remains unchanged
    o_color = grid(R, 1.0, true);  // Main grid
    vec4 smallerGrid = grid(R, 10.0, false);
    smallerGrid.a *= 0.3;  // Make smaller grid less prominent
    o_color = mix(smallerGrid, o_color, o_color.a);
    
    // Apply distance fade
    float fade = smoothstep(0.8, 0.95, compute_fade(R));
    o_color.a *= fade;
    
    gl_FragDepth = compute_depth(R);
}