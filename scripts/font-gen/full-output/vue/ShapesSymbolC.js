import { defineComponent, h } from 'vue';

export const ShapesSymbolC = defineComponent({
  name: 'ShapesSymbolC',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M6.74935 3.10025C6.55994 2.95293 6.29528 2.82774 5.99544 2.7219C4.57125 2.21916 3.25066 3.48968 3.25066 5C3.25066 6.51041 4.57119 7.78116 5.99551 7.27852C6.29532 7.17272 6.55995 7.04756 6.74935 6.90025", "fillRule": "evenodd"})
      ]
    );
  }
});
