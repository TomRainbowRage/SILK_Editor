#version 410 core
layout (location = 0) in vec2 a_position;

out vec3 near;
out vec3 far;

uniform mat4 view;
uniform mat4 projection;

vec3 unproject_point(float x, float y, float z) {
    mat4 viewProjectionInverse = inverse(projection * view);
    vec4 unprojected = viewProjectionInverse * vec4(x, y, z, 1.0);
    return unprojected.xyz / unprojected.w;
}

void main() {
    // Convert from -1,1 space to world space
    near = unproject_point(a_position.x, a_position.y, -1.0);
    far = unproject_point(a_position.x, a_position.y, 1.0);
    gl_Position = vec4(a_position, 0.0, 1.0);
}