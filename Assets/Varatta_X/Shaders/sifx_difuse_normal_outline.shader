Shader "SIFX/Diffuse/Normal/Outline/Base" 
{
	Properties 
	{
		_Radius ("Radius", Range(1,5)) = 5
		_XRayColor("XRay Color", Color) = (1,0,0,1) 
		_MainTex ("Base (RGB)", 2D) = "white" {}
		[NoScaleOffset] _BumpMap ("Normalmap", 2D) = "bump" {}
	}

	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		LOD 250
        Pass  
        {  
            Blend SrcAlpha One  
            ZWrite Off  
            ZTest Greater  
  
			cull back
            CGPROGRAM  
            #include "Lighting.cginc"  
            fixed4 _XRayColor; 
			fixed _Radius;
			
            struct v2f  
            {  
                float4 pos : POSITION;  
                float3 normal : normal;  
                float3 viewDir : TEXCOORD0;
				float4 worldPos : TEXCOORD2;				
            };  
  
            v2f vert (appdata_base v)  
            {  
                v2f o;  
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);  
                o.viewDir = ObjSpaceViewDir(v.vertex);  
                o.normal = v.normal;  
                return o;  
            }  
  
            fixed4 frag(v2f i) : SV_Target  
            {  
			
				float camDist = distance(i.worldPos, _WorldSpaceCameraPos);
                float3 normal = normalize(i.normal);  
                float3 viewDir = normalize(i.viewDir);  
                float rim = 1 - dot(normal, viewDir);  
                //return _XRayColor * rim;  
				fixed alpha = (camDist / _Radius)>_Radius?0:1;
				return half4(_XRayColor.xyz * rim,alpha);
            }  
            #pragma vertex vert  
            #pragma fragment frag  
            ENDCG  
		}
		
	CGPROGRAM
	#pragma surface surf Lambert noforwardadd

	sampler2D _MainTex;
	sampler2D _BumpMap;
	fixed4 _Emissive;
	
	struct Input 
	{
		float2 uv_MainTex;
	};

	void surf (Input IN, inout SurfaceOutput o) 
	{
		fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
		o.Albedo = c.rgb;
		o.Alpha = c.a;
		o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
	}
	
	ENDCG
	}

	FallBack "Mobile/Diffuse"
}
