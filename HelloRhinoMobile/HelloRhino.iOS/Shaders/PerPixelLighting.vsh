attribute vec4 rglVertex;
attribute vec3 rglNormal;
attribute vec4 rglColor;

uniform mat4  rglModelViewProjectionMatrix;
uniform mat3  rglNormalMatrix;
uniform bool  rglUsesColors;

varying vec3  vNormal;
varying vec4  vColor;

void main()
{
  vNormal   = rglNormalMatrix * rglNormal;
  
  if ( rglUsesColors )
    vColor = rglColor;
  else
    vColor = vec4(1.0);
  
  gl_Position = rglModelViewProjectionMatrix * rglVertex;
}