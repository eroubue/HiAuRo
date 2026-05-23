import { defineComponent, h } from 'vue';

export const ShapesSymbolS = defineComponent({
  name: 'ShapesSymbolS',
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
        h('path', {"d": "M6.5487 2.9512C5.6487 2.35118 3.14869 2.45119 3.3487 3.9512C3.54694 5.43804 6.6487 4.55119 6.74869 6.1512C6.84869 7.75122 4.0487 7.55119 3.2487 6.95119", "fillRule": "evenodd"})
      ]
    );
  }
});
