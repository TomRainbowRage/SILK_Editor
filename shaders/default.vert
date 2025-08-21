#version 330 core
layout (location = 0) in vec3 vPos;
layout (location = 1) in vec2 vUv;
layout (location = 2) in vec3 vNormal;
layout (location = 3) in float vFaceIndex;  // New face index attribute

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 fWorldPos;    // World-space position
out vec3 fNormal;      // World-space normal
out vec2 fUv;          // Pass the UV coordinates
out float fFaceIndex;  // Pass the face index to the fragment shader

void main()
{
    gl_Position = uProjection * uView * uModel * vec4(vPos, 1.0);
    fWorldPos = vec3(uModel * vec4(vPos, 1.0));
    fNormal = mat3(transpose(inverse(uModel))) * vNormal;
    fUv = vUv;
    fFaceIndex = vFaceIndex;  // Pass the face index
}