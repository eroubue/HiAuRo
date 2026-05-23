import { defineComponent, h } from 'vue';

export const ShapesSymbolMultiplication3 = defineComponent({
  name: 'ShapesSymbolMultiplication3',
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
        h('path', {"d": "M3.7 5C3.7 5.71797 4.28203 6.3 5 6.3C5.71797 6.3 6.3 5.71797 6.3 5C6.3 4.28203 5.71797 3.7 5 3.7C4.28203 3.7 3.7 4.28203 3.7 5Z", "fillRule": "evenodd"})
      ]
    );
  }
});
