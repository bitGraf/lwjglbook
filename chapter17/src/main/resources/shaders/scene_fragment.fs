#version 330

const int MAX_POINT_LIGHTS = 5;
const int MAX_SPOT_LIGHTS = 5;

in vec2 outTexCoord;
in vec3 mvVertexNormal;
in vec3 mvVertexPos;
in mat4 outModelViewMatrix;

out vec4 fragColor;

struct Attenuation
{
    float constant;
    float linear;
    float exponent;
};

struct PointLight
{
    vec3 colour;
    // Light position is assumed to be in view coordinates
    vec3 position;
    float intensity;
    Attenuation att;
};

struct SpotLight
{
    PointLight pl;
    vec3 conedir;
    float cutoff;
};

struct DirectionalLight
{
    vec3 colour;
    vec3 direction;
    float intensity;
};

struct Material
{
    vec3 colour;
    int hasTexture;
    int hasNormalMap;
    float reflectance;
};

struct Fog
{
    int active;
    vec3 colour;
    float density;
};

uniform sampler2D texture_sampler;
uniform sampler2D normalMap;
uniform vec3 ambientLight;
uniform float specularPower;
uniform Material material;
uniform PointLight pointLights[MAX_POINT_LIGHTS];
uniform SpotLight spotLights[MAX_SPOT_LIGHTS];
uniform DirectionalLight directionalLight;
uniform vec3 camera_pos;
uniform Fog fog;

vec4 calcLightColour(vec3 light_colour, float light_intensity, vec3 position, vec3 to_light_dir, vec3 normal)
{
    vec4 diffuseColour = vec4(0, 0, 0, 0);
    vec4 specColour = vec4(0, 0, 0, 0);

    // Diffuse Light
    float diffuseFactor = max(dot(normal, to_light_dir), 0.0);
    diffuseColour = vec4(light_colour, 1.0) * light_intensity * diffuseFactor;

    // Specular Light
    vec3 camera_direction = normalize(camera_pos - position);
    vec3 from_light_dir = -to_light_dir;
    vec3 reflected_light = normalize(reflect(from_light_dir , normal));
    float specularFactor = max( dot(camera_direction, reflected_light), 0.0);
    specularFactor = pow(specularFactor, specularPower);
    specColour = light_intensity  * specularFactor * material.reflectance * vec4(light_colour, 1.0);

    return (diffuseColour + specColour);
}

vec4 calcPointLight(PointLight light, vec3 position, vec3 normal)
{
    vec3 light_direction = light.position - position;
    vec3 to_light_dir  = normalize(light_direction);
    vec4 light_colour = calcLightColour(light.colour, light.intensity, position, to_light_dir, normal);

    // Apply Attenuation
    float distance = length(light_direction);
    float attenuationInv = light.att.constant + light.att.linear * distance +
        light.att.exponent * distance * distance;
    return light_colour / attenuationInv;
}

vec4 calcSpotLight(SpotLight light, vec3 position, vec3 normal)
{
    vec3 light_direction = light.pl.position - position;
    vec3 to_light_dir  = normalize(light_direction);
    vec3 from_light_dir  = -to_light_dir;
    float spot_alfa = dot(from_light_dir, normalize(light.conedir));
    
    vec4 colour = vec4(0, 0, 0, 0);
    
    if ( spot_alfa > light.cutoff ) 
    {
        colour = calcPointLight(light.pl, position, normal);
        colour *= (1.0 - (1.0 - spot_alfa)/(1.0 - light.cutoff));
    }
    return colour;    
}

vec4 calcDirectionalLight(DirectionalLight light, vec3 position, vec3 normal)
{
    return calcLightColour(light.colour, light.intensity, position, normalize(light.direction), normal);
}

vec4 calcFog(vec3 pos, vec4 colour, Fog fog, vec3 ambientLight, DirectionalLight dirLight)
{
    vec3 fogColor = fog.colour * (ambientLight + dirLight.colour * dirLight.intensity);
    float distance = length(pos);
    float fogFactor = 1.0 / exp( (distance * fog.density)* (distance * fog.density));
    fogFactor = clamp( fogFactor, 0.0, 1.0 );

    vec3 resultColour = mix(fogColor, colour.xyz, fogFactor);
    return vec4(resultColour.xyz, 1);
}

vec4 calcBaseColour(Material material, vec2 text_coord)
{
    vec4 baseColour;
    if ( material.hasTexture == 1 )
    {
        baseColour = texture(texture_sampler, text_coord);
    }
    else
    {
        baseColour = vec4(material.colour, 1);
    }
    return baseColour;
}

vec3 calcNormal(Material material, vec3 normal, vec2 text_coord, mat4 modelViewMatrix)
{
    vec3 newNormal = normal;
    if ( material.hasNormalMap == 1 )
    {
        newNormal = texture(normalMap, text_coord).rgb;
        newNormal = normalize(newNormal * 2 - 1);
        newNormal = normalize(modelViewMatrix * vec4(newNormal, 0.0)).xyz;
    }
    return newNormal;
}

void main()
{
    vec4 baseColour = calcBaseColour(material, outTexCoord);
    vec3 currNomal = calcNormal(material, mvVertexNormal, outTexCoord, outModelViewMatrix);

    vec4 totalLight = vec4(ambientLight, 1.0);
    totalLight += calcDirectionalLight(directionalLight, mvVertexPos, currNomal);

    for (int i=0; i<MAX_POINT_LIGHTS; i++)
    {
        if ( pointLights[i].intensity > 0 )
        {
            totalLight += calcPointLight(pointLights[i], mvVertexPos, currNomal); 
        }
    }

    for (int i=0; i<MAX_SPOT_LIGHTS; i++)
    {
        if ( spotLights[i].pl.intensity > 0 )
        {
            totalLight += calcSpotLight(spotLights[i], mvVertexPos, currNomal);
        }
    }
    
    fragColor = baseColour * clamp(totalLight,0, 1);

    if ( fog.active == 1 ) 
    {
        fragColor = calcFog(mvVertexPos, fragColor, fog, ambientLight, directionalLight);
    }
}